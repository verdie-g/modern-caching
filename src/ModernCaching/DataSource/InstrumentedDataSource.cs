using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using ModernCaching.Instrumentation;

namespace ModernCaching.DataSource
{
    /// <summary>Wraps an <see cref="IDataSource{TKey,TValue}"/> with metrics.</summary>
    internal class InstrumentedDataSource<TKey, TValue> : IDataSource<TKey, TValue>
    {
        private readonly IDataSource<TKey, TValue> _dataSource;
        private readonly ICacheMetrics _metrics;
        private readonly ILogger? _logger;

        public InstrumentedDataSource(IDataSource<TKey, TValue> dataSource, ICacheMetrics metrics, ILogger? logger)
        {
            _dataSource = dataSource;
            _metrics = metrics;
            _logger = logger;
        }

        public async IAsyncEnumerable<DataSourceResult<TKey, TValue>> LoadAsync(IEnumerable<TKey> keys,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                string keysStr = string.Join(", ", keys);
                _logger.Log(LogLevel.Trace, "IDataSource: LOAD [{0}]", keysStr);
            }

            IAsyncEnumerator<DataSourceResult<TKey, TValue>> results;
            try
            {
                results = _dataSource.LoadAsync(keys, cancellationToken)?.GetAsyncEnumerator(cancellationToken)
                          ?? throw new NullReferenceException($"{nameof(IDataSource<TKey, TValue>.LoadAsync)} returned null");
                _metrics.IncrementDataSourceLoadOk();
            }
            catch (Exception e)
            {
                _metrics.IncrementDataSourceLoadError();
                _logger?.LogError(e, "An error occured loading keys from source");
                throw;
            }

            int errors = 0;
            while (true)
            {
                DataSourceResult<TKey, TValue> dataSourceResult;
                try
                {
                    if (!await results.MoveNextAsync())
                    {
                        break;
                    }

                    dataSourceResult = results.Current;

                    if (dataSourceResult == null)
                    {
                        throw new NullReferenceException($"A null {nameof(DataSourceResult<TKey, TValue>)} was returned"
                                                         + $" by {nameof(IDataSource<TKey, TValue>.LoadAsync)}");
                    }

                    if (dataSourceResult.TimeToLive < TimeSpan.Zero)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(dataSourceResult.TimeToLive),
                            dataSourceResult.TimeToLive,
                            "The time-to-live value must be positive.");
                    }
                }
                catch (Exception e)
                {
                    errors += 1;
                    _logger?.LogError(e, $"An error occured while iterating on {nameof(IDataSource<TKey, TValue>.LoadAsync)} result");
                    continue;
                }

                yield return dataSourceResult;
            }

            await results.DisposeAsync();
            _metrics.IncrementDataSourceKeyLoadErrors(errors);
        }
    }
}
