using System.Diagnostics.CodeAnalysis;

namespace ModernCaching.LocalCaching
{
    /// <summary>
    /// Cache that gets its key synchronously. Typically an in-memory cache.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the cache.</typeparam>
    /// <typeparam name="TValue">The type of the values in the cache entries.</typeparam>
    public interface ICache<in TKey, TValue>
    {
        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the element to get.</param>
        /// <param name="entry">Entry associated with the specified key, if the key is found; otherwise, the value is
        /// set to the default for the type of the value parameter. Existing entries should be returned even if stale.
        /// If the cache is evicting, the implementation should have a grace period during which stale entries are kept
        /// in memory so that <see cref="IReadOnlyCache{TKey,TValue}.TryPeek"/> can return a stale value during its
        /// refresh. <see cref="CacheEntry{TValue}.Value"/> can be null if the data source returns null.</param>
        /// <returns>True if the cache contains an entry with the specified key; otherwise, false.</returns>
        /// <remarks>This method should never throw.</remarks>
        bool TryGet(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue?> entry);

        /// <summary>
        /// Sets the entry associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the element to set.</param>
        /// <param name="entry">The entry associated with the specified key.</param>
        /// <remarks>This method should never throw.</remarks>
        void Set(TKey key, CacheEntry<TValue?> entry);
    }
}
