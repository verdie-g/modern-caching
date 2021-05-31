using System;
using System.Threading.Tasks;

namespace ModernCaching.DistributedCaching
{
    /// <summary>
    /// Cache that gets its keys asynchronously. Typically a distributed cache like Memcached or Redis. Implementations
    /// should only wrap a specific technology and shouldn't depend on a specific <see cref="IReadOnlyCache{TKey,TValue}"/>
    /// so the same <see cref="IAsyncCache"/> instance can be reused between several <see cref="IReadOnlyCache{TKey,TValue}"/>.
    /// </summary>
    public interface IAsyncCache
    {
        /// <summary>
        /// Gets a value with the given key.
        /// </summary>
        /// <param name="key">The key of the element to get.</param>
        Task<AsyncCacheResult> GetAsync(string key);

        /// <summary>
        /// Sets the value with the given key.
        /// </summary>
        /// <param name="key">The key of the element to set.</param>
        /// <param name="value">The value of the element to set.</param>
        /// <param name="timeToLive">Duration after which the element is considered stale.</param>
        Task SetAsync(string key, byte[] value, TimeSpan timeToLive);

        /// <summary>
        /// Removes the value with the given key.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        Task RemoveAsync(string key);
    }
}
