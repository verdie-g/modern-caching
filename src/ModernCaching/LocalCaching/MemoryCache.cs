using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ModernCaching.Utils;

namespace ModernCaching.LocalCaching;

/// <summary>
/// Implementation of a <see cref="ICache{TKey,TValue}"/> using a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// The entries in this cache are never evicted. It can be a good thing to avoid gen 2 fragmentation.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
public sealed class MemoryCache<TKey, TValue> : ICache<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, CacheEntry<TValue>> _dictionary;

    // Avoid using ConcurrentDictionary.Count that locks the entire dictionary.
    private readonly HighReadLowWriteCounter _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryCache{TKey,TValue}"/> class.
    /// </summary>
    public MemoryCache()
    {
        _dictionary = new ConcurrentDictionary<TKey, CacheEntry<TValue>>();
        _count = new HighReadLowWriteCounter();
    }

    /// <inheritdoc />
    public int Count => (int)_count.Value;

    /// <inheritdoc />
    public bool TryGet(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue> entry)
    {
        return _dictionary.TryGetValue(key, out entry);
    }

    /// <inheritdoc />
    public void Set(TKey key, CacheEntry<TValue> entry)
    {
        if (_dictionary.TryGetValue(key, out CacheEntry<TValue>? existingEntry))
        {
            _dictionary.TryUpdate(key, entry, existingEntry);
        }
        else if (_dictionary.TryAdd(key, entry))
        {
            _count.Increment();
        }
    }

    /// <inheritdoc />
    public bool TryDelete(TKey key)
    {
        if (!_dictionary.TryRemove(key, out _))
        {
            return false;
        }

        _count.Decrement();
        return true;
    }
}
