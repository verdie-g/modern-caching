using System.Diagnostics.CodeAnalysis;

namespace ModernCaching.LocalCaching
{
    public interface ICache<in TKey, TValue>
    {
        // Should never throw.
        bool TryGet(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue> value);
        void Set(TKey key, CacheEntry<TValue> value);
    }
}
