using System;

namespace ModernCaching.LocalCaching
{
    /// <summary>
    /// Entry of an <see cref="ICache{TKey,TValue}"/>.
    /// </summary>
    /// <param name="Value">The value of the entry.</param>
    /// <param name="ExpirationTime">The UTC time after which the value is considered stale.</param>
    /// <param name="GraceTime">The UTC time until which the entry can be kept in the cache.</param>
    public record CacheEntry<TValue>(TValue? Value, DateTime ExpirationTime, DateTime GraceTime);
}
