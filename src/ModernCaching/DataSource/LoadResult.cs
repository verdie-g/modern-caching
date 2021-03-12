using System;

namespace ModernCaching.DataSource
{
    public class LoadResult<TKey, TValue>
    {
        public TKey Key { get; }
        public TValue Value { get; }
        public TimeSpan TimeToLive { get; }

        public LoadResult(TKey key, TValue value, TimeSpan timeToLive)
        {
            Key = key;
            Value = value;
            TimeToLive = timeToLive;
        }
    }
}
