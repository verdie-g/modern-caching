using System;

namespace ModernCaching.LocalCaching
{
    /// <summary>
    /// Entry of an <see cref="ICache{TKey,TValue}"/>.
    /// </summary>
    public class CacheEntry<TValue>
    {
        private readonly TValue _value;

        /// <summary>Instantiates a <see cref="CacheEntry{TValue}"/> with a value.</summary>
        internal CacheEntry(TValue value)
        {
            HasValue = true;
            _value = value;
        }

        /// <summary>Instantiates a <see cref="CacheEntry{TValue}"/> without a value.</summary>
        internal CacheEntry()
        {
            HasValue = false;
            _value = default!;
        }

        /// <summary>Whether the current <see cref="CacheEntry{TValue}"/> has a value.</summary>
        public bool HasValue { get; }

        /// <summary>The value of the current entry if <see cref="HasValue"/> is true; otherwise an exception.</summary>
        /// <exception cref="InvalidOperationException"><see cref="HasValue"/> is false.</exception>
        public TValue Value => HasValue
            ? _value
            : throw new InvalidOperationException("Cache entry object must have a value.");

        /// <summary>The UTC time after which the value is considered stale.</summary>
        public DateTime ExpirationTime { get; internal set; }

        /// <summary>The UTC time after which the entry should get evicted (if the cache is evicting).</summary>
        public DateTime EvictionTime { get; internal set; }

        /// <summary>Retrieves the value or the default value of the underlying type.</summary>
        public TValue GetValueOrDefault()
        {
            return _value;
        }

        public override string ToString()
        {
            return HasValue && Value != null ? Value.ToString() : string.Empty;
        }
    }
}
