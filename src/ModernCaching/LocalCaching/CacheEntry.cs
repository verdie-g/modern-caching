using System;

namespace ModernCaching.LocalCaching
{
    /// <summary>
    /// Entry of an <see cref="ICache{TKey,TValue}"/>.
    /// </summary>
    /// <param name="Value">The value of the entry.</param>
    /// <param name="ExpirationTime">The UTC time after which the value is considered stale.</param>
    /// <param name="EvictionTime">The UTC time after which the entry should get evicted (if the cache is evicting).</param>
    public record CacheEntry<TValue>(TValue Value, DateTime ExpirationTime, DateTime EvictionTime);
}
