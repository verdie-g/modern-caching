using System;
using System.Threading.Tasks;

namespace ModernCaching.DistributedCaching;

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
    /// <remarks>
    /// The method does not take a time-to-live the for key since some technologies don't support them at the key level
    /// (e.g. S3). Instead a hard time-to-live should be used in the <see cref="IAsyncCache"/> implementation and the
    /// <see cref="IReadOnlyCache{TKey,TValue}"/> uses a soft on in each cache entry.
    /// </remarks>
    Task SetAsync(string key, byte[] value);

    /// <summary>
    /// Deletes the value with the given key.
    /// </summary>
    /// <param name="key">The key of the element to delete.</param>
    Task DeleteAsync(string key);
}
