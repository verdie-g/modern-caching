using System.Collections.Generic;
using System.Threading;
using ModernCaching.Instrumentation;

namespace ModernCaching.DataSource
{
    /// <summary>Wraps an <see cref="IDataSource{TKey,TValue}"/> with metrics.</summary>
    internal class InstrumentedDataSource<TKey, TValue> : IDataSource<TKey, TValue>
    {
        private readonly IDataSource<TKey, TValue> _dataSource;
        private readonly ICacheMetrics _metrics;

        public InstrumentedDataSource(IDataSource<TKey, TValue> dataSource, ICacheMetrics metrics)
        {
            _dataSource = dataSource;
            _metrics = metrics;
        }

        public IAsyncEnumerable<DataSourceResult<TKey, TValue?>> LoadAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken)
        {
            try
            {
                var results = _dataSource.LoadAsync(keys, cancellationToken);
                _metrics.IncrementDataSourceLoadOk();
                return results;
            }
            catch
            {
                _metrics.IncrementDataSourceLoadError();
                throw;
            }
        }
    }
}
