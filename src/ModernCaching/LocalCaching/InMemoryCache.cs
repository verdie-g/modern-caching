using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ModernCaching.LocalCaching
{
    /// <summary>
    /// Implementation of a <see cref="ICache{TKey,TValue}"/> using a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// The entries in this cache are never evicted. It can be a good thing to avoid gen 2 fragmentation.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    public class InMemoryCache<TKey, TValue> : ICache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry<TValue?>> _dictionary;

        public InMemoryCache() => _dictionary = new ConcurrentDictionary<TKey, CacheEntry<TValue?>>(GetComparer());
        public bool TryGet(TKey key, out CacheEntry<TValue?> entry) => _dictionary.TryGetValue(key, out entry);
        public void Set(TKey key, CacheEntry<TValue?> entry) => _dictionary[key] = entry;

        private IEqualityComparer<TKey?> GetComparer()
        {
            // Special case for string since it's a common key type.
            if (typeof(TKey) == typeof(string))
            {
                return (IEqualityComparer<TKey?>)StringComparer.Ordinal;
            }

            return EqualityComparer<TKey?>.Default;
        }
    }
}
