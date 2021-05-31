using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModernCaching.Instrumentation;

namespace ModernCaching.DistributedCaching
{
    /// <summary>Wraps an <see cref="IAsyncCache"/> with metrics.</summary>
    internal class InstrumentedAsyncCache : IAsyncCache
    {
        private readonly IAsyncCache _cache;
        private readonly ICacheMetrics _metrics;
        private readonly ILogger? _logger;

        public InstrumentedAsyncCache(IAsyncCache cache, ICacheMetrics metrics, ILogger? logger)
        {
            _cache = cache;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task<AsyncCacheResult> GetAsync(string key)
        {
            AsyncCacheResult res;
            try
            {
                res = await _cache.GetAsync(key);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "An error occured getting key '{key}' from distributed cache", key);
                throw;
            }

            switch (res.Status)
            {
                case AsyncCacheStatus.Hit:
                    _metrics.IncrementDistributedCacheGetHits();
                    break;
                case AsyncCacheStatus.Miss:
                    _metrics.IncrementDistributedCacheGetMisses();
                    break;
                case AsyncCacheStatus.Error:
                    _metrics.IncrementDistributedCacheGetErrors();
                    break;
            }

            return res;
        }

        public Task SetAsync(string key, byte[] value, TimeSpan timeToLive)
        {
            _metrics.IncrementDistributedCacheSet();
            return _cache.SetAsync(key, value, timeToLive);
        }

        public Task RemoveAsync(string key)
        {
            _metrics.IncrementDistributedCacheRemove();
            return _cache.RemoveAsync(key);
        }
    }
}
