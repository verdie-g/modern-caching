﻿using System;
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

        public async IAsyncEnumerable<DataSourceResult<TKey, TValue?>> LoadAsync(IEnumerable<TKey> keys,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IAsyncEnumerator<DataSourceResult<TKey, TValue?>> results;
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
                DataSourceResult<TKey, TValue?> dataSourceResult;
                try
                {
                    if (!await results.MoveNextAsync())
                    {
                        break;
                    }

                    dataSourceResult = results.Current;
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
