using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ModernCaching.Utils
{
    internal sealed class ConcurrentDictionaryHelper
    {
        public static ConcurrentDictionary<TKey, TValue> Create<TKey, TValue>()
        {
            // Special case for string since it's a common key type.
            var comparer = typeof(TKey) == typeof(string)
                ? (IEqualityComparer<TKey?>)StringComparer.Ordinal
                : EqualityComparer<TKey?>.Default;

            return new ConcurrentDictionary<TKey, TValue>(comparer);
        }
    }
}
