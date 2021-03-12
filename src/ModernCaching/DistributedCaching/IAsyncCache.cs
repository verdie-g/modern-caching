using System;
using System.Threading.Tasks;

namespace ModernCaching.DistributedCaching
{
    public interface IAsyncCache
    {
        // Should never throw.
        Task<AsyncCacheResult> GetAsync(string key);
        Task SetAsync(string key, byte[] value, TimeSpan timeToLive);
    }
}
