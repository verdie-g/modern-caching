using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ModernCaching.Instrumentation;

namespace ModernCaching.LocalCaching
{
    /// <summary>Wraps an <see cref="ICache{TKey,TValue}"/> with metrics.</summary>
    internal class InstrumentedCache<TKey, TValue> : ICache<TKey, TValue> where TKey : IEquatable<TKey>
    {
        private readonly ICache<TKey, TValue> _cache;
        private readonly ICacheMetrics _metrics;
        private readonly ILogger? _logger;

        public InstrumentedCache(ICache<TKey, TValue> cache, ICacheMetrics metrics, ILogger? logger)
        {
            _cache = cache;
            _metrics = metrics;
            _logger = logger;

            metrics.SetLocalCacheCountPoller(() => Count);
        }

        /// <inheritdoc />
        public int Count => _cache.Count;

        /// <inheritdoc />
        public bool TryGet(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue> entry)
        {
            bool found;
            if (_cache.TryGet(key, out entry))
            {
                _metrics.IncrementLocalCacheGetHits();
                found = true;
            }
            else
            {
                _metrics.IncrementLocalCacheGetMisses();
                found = false;
            }

            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                if (found)
                {
                    _logger.Log(LogLevel.Trace, "ICache     : GET  {0} -> HIT {1}", key, entry!.Value);
                }
                else
                {
                    _logger.Log(LogLevel.Trace, "ICache     : GET  {0} -> MISS", key);
                }
            }

            return found;
        }

        /// <inheritdoc />
        public void Set(TKey key, CacheEntry<TValue> entry)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.Log(LogLevel.Trace, "ICache     : SET  {0} {1}", key, entry.Value);
            }

            _metrics.IncrementLocalCacheSet();
            _cache.Set(key, entry);
        }

        /// <inheritdoc />
        public void Delete(TKey key)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.Log(LogLevel.Trace, "ICache     : DEL  {0}", key);
            }

            _metrics.IncrementLocalCacheDelete();
            _cache.Delete(key);
        }
    }
}
