using System;

namespace ModernCaching.DataSource
{
    public record LoadResult<TKey, TValue>(TKey Key, TValue Value, TimeSpan TimeToLive);
}
