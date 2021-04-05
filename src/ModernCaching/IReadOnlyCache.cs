using System;
using System.Threading.Tasks;

namespace ModernCaching
{
    /// <summary>
    /// A 2-layer caching solution in front of a data source.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the cache.</typeparam>
    /// <typeparam name="TValue">The type of the values in the cache.</typeparam>
    public interface IReadOnlyCache<in TKey, TValue> : IDisposable where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets the value associated with the specified key from the local cache and the local cache only. Reloads the
        /// value in the background if it's stale. Use this method if getting stale values is not an issue.
        /// </summary>
        /// <param name="key">The key of the element to get.</param>
        /// <param name="value">Value associated with the specified key, if the key is found; otherwise, the default
        /// value for the type of the value parameter. Can be null if the data source returns null.</param>
        /// <returns>True if the local cache contains an element with the specified key; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException"><see cref="key"/> is null.</exception>
        bool TryGet(TKey key, out TValue value);

        /// <summary>
        /// Gets the first fresh value associated with the specified key from, in order, the local cache, the distributed
        /// cache, the data source, with backfilling. The freshness of the value is guaranteed only if the distributed
        /// cache and the data source are available. Use this method if getting fresh values is important, at the cost
        /// of extra load on the data source.
        /// </summary>
        /// <param name="key">The key of the element to get.</param>
        /// <returns>(true, not default) if a fresh value was found; otherwise, (false, default). The value can be null
        /// if the data source returns null.</returns>
        /// <exception cref="ArgumentNullException"><see cref="key"/> is null.</exception>
        ValueTask<(bool found, TValue value)> TryGetAsync(TKey key);
    }
}
