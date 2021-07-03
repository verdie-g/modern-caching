using System;

namespace ModernCaching.LocalCaching
{
    /// <summary>
    /// Entry of an <see cref="ICache{TKey,TValue}"/>.
    /// </summary>
    /// <param name="Value">The value of the entry.</param>
    public record CacheEntry<TValue>(TValue Value)
    {
        /// <summary>The UTC time after which the value is considered stale.</summary>
        public DateTime ExpirationTime { get; internal set; }

        /// <summary>The UTC time after which the entry should get evicted (if the cache is evicting).</summary>
        public DateTime EvictionTime { get; internal set; }
    }
}
