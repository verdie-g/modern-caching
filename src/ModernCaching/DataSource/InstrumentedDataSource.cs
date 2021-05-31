using System;
using System.Collections.Generic;
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

        public IAsyncEnumerable<DataSourceResult<TKey, TValue?>> LoadAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken)
        {
            try
            {
                var results = _dataSource.LoadAsync(keys, cancellationToken);
                _metrics.IncrementDataSourceLoadOk();
                return results;
            }
            catch (Exception e)
            {
                _metrics.IncrementDataSourceLoadError();
                _logger?.LogError(e, "An error occured loading keys from source");
                throw;
            }
        }
    }
}
