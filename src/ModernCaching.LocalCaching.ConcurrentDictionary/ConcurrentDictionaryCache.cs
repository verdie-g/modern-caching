using System.Collections.Concurrent;

namespace ModernCaching.LocalCaching.ConcurrentDictionary
{
    /// <summary>
    /// Implementation of a <see cref="ICache{TKey,TValue}"/> using a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// The entries in this cache are never evicted. It can be a good thing to avoid gen 2 fragmentation.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    public class ConcurrentDictionaryCache<TKey, TValue> : ICache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry<TValue>> _dictionary;

        public ConcurrentDictionaryCache() => _dictionary = new ConcurrentDictionary<TKey, CacheEntry<TValue>>();
        public bool TryGet(TKey key, out CacheEntry<TValue> entry) => _dictionary.TryGetValue(key, out entry);
        public void Set(TKey key, CacheEntry<TValue> entry) => _dictionary[key] = entry;
    }
}
