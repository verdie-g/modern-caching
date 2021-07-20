using System;
using System.Diagnostics.CodeAnalysis;

namespace ModernCaching.LocalCaching
{
    /// <summary>
    /// Cache that gets its key synchronously. Typically an in-memory cache.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the cache.</typeparam>
    /// <typeparam name="TValue">The type of the values in the cache entries.</typeparam>
    /// <remarks>If the cache is evicting, <see cref="CacheEntry{TValue}.EvictionTime"/> should be used for the eviction time.</remarks>
    public interface ICache<in TKey, TValue> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets the number of entries contained in the cache.
        /// </summary>
        /// <remarks>
        /// It is preferred that the implementation returns an approximate value rather than locking the entire cache.
        /// </remarks>
        int Count { get; }

        /// <summary>
        /// Gets the entry associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the element to get.</param>
        /// <param name="entry">Entry associated with the specified key, if the key is found; otherwise, the value is
        /// set to the default for the type of the value parameter. Existing entries should be returned even if stale.
        /// <see cref="CacheEntry{TValue}.Value"/> can be null if the data source returns null.</param>
        /// <returns>True if the cache contains an entry with the specified key; otherwise, false.</returns>
        /// <remarks>This method should never throw.</remarks>
        bool TryGet(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue> entry);

        /// <summary>
        /// Sets the entry associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the element to set.</param>
        /// <param name="entry">The entry associated with the specified key.</param>
        /// <remarks>This method should never throw.</remarks>
        void Set(TKey key, CacheEntry<TValue> entry);

        /// <summary>
        /// Deletes the entry with the specified key.
        /// </summary>
        /// <param name="key">The key of the entry to delete.</param>
        void Delete(TKey key);
    }
}
