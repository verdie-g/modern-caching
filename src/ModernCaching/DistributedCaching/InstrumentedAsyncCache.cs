using System;
using System.Threading.Tasks;
using ModernCaching.Instrumentation;

namespace ModernCaching.DistributedCaching
{
    /// <summary>Wraps an <see cref="IAsyncCache"/> with metrics.</summary>
    internal class InstrumentedAsyncCache : IAsyncCache
    {
        private readonly IAsyncCache _cache;
        private readonly ICacheMetrics _metrics;

        public InstrumentedAsyncCache(IAsyncCache cache, ICacheMetrics metrics)
        {
            _cache = cache;
            _metrics = metrics;
        }

        public async Task<AsyncCacheResult> GetAsync(string key)
        {
            var res = await _cache.GetAsync(key);
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
