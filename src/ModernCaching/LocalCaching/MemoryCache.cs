using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ModernCaching.Utils;

namespace ModernCaching.LocalCaching
{
    /// <summary>
    /// Implementation of a <see cref="ICache{TKey,TValue}"/> using a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// The entries in this cache are never evicted. It can be a good thing to avoid gen 2 fragmentation.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    public class MemoryCache<TKey, TValue> : ICache<TKey, TValue> where TKey : IEquatable<TKey>
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry<TValue>> _dictionary;

        /// <summary>
        /// Eventually consistent count of <see cref="_dictionary"/>. This count is increment/decremented using
        /// <see cref="Interlocked.Increment"/> to avoid using <see cref="ConcurrentDictionary{TKey,TValue}.Count"/>
        /// that locks the entire dictionary.</summary>
        private int _count;

        public MemoryCache()
        {
            _dictionary = ConcurrentDictionaryHelper.Create<TKey, CacheEntry<TValue>>();
            _count = 0;
        }

        /// <inheritdoc />
        public int Count => _count;

        /// <inheritdoc />
        public bool TryGet(TKey key, out CacheEntry<TValue> entry)
        {
            return _dictionary.TryGetValue(key, out entry);
        }

        /// <inheritdoc />
        public void Set(TKey key, CacheEntry<TValue> entry)
        {
            if (_dictionary.TryGetValue(key, out CacheEntry<TValue> existingEntry))
            {
                _dictionary.TryUpdate(key, entry, existingEntry);
            }
            else
            {
                _dictionary.TryAdd(key, entry);
                Interlocked.Increment(ref _count);
            }
        }

        /// <inheritdoc />
        public void Delete(TKey key)
        {
            if (_dictionary.Remove(key, out _))
            {
                Interlocked.Decrement(ref _count);
            }
        }
    }
}
