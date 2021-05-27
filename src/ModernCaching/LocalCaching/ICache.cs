using System.Diagnostics.CodeAnalysis;

namespace ModernCaching.LocalCaching
{
    /// <summary>
    /// Cache that gets its key synchronously. Typically an in-memory cache.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public interface ICache<in TKey, TValue>
    {
        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the element to get.</param>
        /// <param name="value">Value associated with the specified key, if the key is found; otherwise, the default
        /// value for the type of the value parameter. Existing value should be returned even if stale. <see cref="CacheEntry{TValue}.Value"/>
        /// can be null if the data source returns null.</param>
        /// <returns>True if the cache contains an element with the specified key; otherwise, false.</returns>
        /// <remarks>This method should never throw.</remarks>
        bool TryGet(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue?> value);

        /// <summary>
        /// Sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the element to set.</param>
        /// <param name="value">The value associated with the specified key.</param>
        /// <remarks>This method should never throw.</remarks>
        void Set(TKey key, CacheEntry<TValue?> value);
    }
}
