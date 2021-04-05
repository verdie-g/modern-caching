using System;

namespace ModernCaching.LocalCaching
{
    public record CacheEntry<TValue>(TValue Value, DateTime ExpirationTime);
}
