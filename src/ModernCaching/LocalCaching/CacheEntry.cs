using System;

namespace ModernCaching.LocalCaching
{
    public class CacheEntry<TValue>
    {
        public TValue Value { get; }
        public DateTime ExpirationTime { get; }

        public CacheEntry(TValue value, DateTime expirationTime)
        {
            Value = value;
            ExpirationTime = expirationTime;
        }
    }
}
