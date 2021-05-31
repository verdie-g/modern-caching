using System.Diagnostics.CodeAnalysis;
using ModernCaching.Instrumentation;

namespace ModernCaching.LocalCaching
{
    /// <summary>Wraps an <see cref="ICache{TKey,TValue}"/> with metrics.</summary>
    internal class InstrumentedCache<TKey, TValue> : ICache<TKey, TValue>
    {
        private readonly ICache<TKey, TValue> _cache;
        private readonly ICacheMetrics _metrics;

        public InstrumentedCache(ICache<TKey, TValue> cache, ICacheMetrics metrics)
        {
            _cache = cache;
            _metrics = metrics;
        }

        public bool TryGet(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue?> entry)
        {
            if (_cache.TryGet(key, out entry))
            {
                _metrics.IncrementLocalCacheGetHits();
                return true;
            }
            else
            {
                _metrics.IncrementLocalCacheGetMisses();
                return false;
            }
        }

        public void Set(TKey key, CacheEntry<TValue?> entry)
        {
            _metrics.IncrementLocalCacheSet();
            _cache.Set(key, entry);
        }

        public void Remove(TKey key)
        {
            _metrics.IncrementLocalCacheRemove();
            _cache.Remove(key);
        }
    }
}
