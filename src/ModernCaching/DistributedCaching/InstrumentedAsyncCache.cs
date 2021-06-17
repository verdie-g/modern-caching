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

            bool shouldLog = _logger != null && _logger.IsEnabled(LogLevel.Trace);
            switch (res.Status)
            {
                case AsyncCacheStatus.Hit:
                    _metrics.IncrementDistributedCacheGetHits();
                    if (shouldLog)
                    {
                        _logger.Log(LogLevel.Trace, "IAsyncCache: GET {0} -> HIT {1}B", key, res.Value!.Length);
                    }
                    break;
                case AsyncCacheStatus.Miss:
                    _metrics.IncrementDistributedCacheGetMisses();
                    if (shouldLog)
                    {
                        _logger.Log(LogLevel.Trace, "IAsyncCache: GET  {0} -> MISS", key);
                    }
                    break;
                case AsyncCacheStatus.Error:
                    _metrics.IncrementDistributedCacheGetErrors();
                    if (shouldLog)
                    {
                        _logger.Log(LogLevel.Trace, "IAsyncCache: GET  {0} -> ERROR", key);
                    }
                    break;
            }

            return res;
        }

        public Task SetAsync(string key, byte[] value, TimeSpan timeToLive)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.Log(LogLevel.Trace, "IAsyncCache: SET  {0} {1}B {2}", key, value.Length, timeToLive);
            }

            _metrics.IncrementDistributedCacheSet();
            return _cache.SetAsync(key, value, timeToLive);
        }

        public Task DeleteAsync(string key)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
            {
                _logger.Log(LogLevel.Trace, "IAsyncCache: DEL  {0}", key);
            }

            _metrics.IncrementDistributedCacheDelete();
            return _cache.DeleteAsync(key);
        }
    }
}
