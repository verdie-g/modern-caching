using System;
using System.IO;
using System.Threading.Tasks;
using ModernCaching.LocalCaching;

namespace ModernCaching.DistributedCaching
{
    internal interface IDistributedCache<in TKey, TValue> where TKey : IEquatable<TKey>
    {
        /// <summary>Gets the entry associated with the specified key from the distributed cache.</summary>
        Task<(AsyncCacheStatus status, CacheEntry<TValue?>? entry)> GetAsync(TKey key);

        /// <summary>Sets the specified key and entry to the distributed cache.</summary>
        Task SetAsync(TKey key, CacheEntry<TValue?> entry);
    }

    /// <summary>
    /// Internal class that wraps a generic <see cref="IAsyncCache"/> with a <see cref="IKeyValueSerializer{TKey,TValue}"/>
    /// for a specific <see cref="ReadOnlyCache{TKey,TValue}"/>.
    /// </summary>
    internal class DistributedCache<TKey, TValue> : IDistributedCache<TKey, TValue> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Name of cache. Used in the distributed cache key.
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// The generic distributed cache. Not bound to a specific <see cref="ReadOnlyCache{TKey,TValue}"/>.
        /// </summary>
        private readonly IAsyncCache _cache;

        /// <summary>
        /// Serializer for the <see cref="_cache"/>. Bound to a specific <see cref="ReadOnlyCache{TKey,TValue}"/>.
        /// </summary>
        private readonly IKeyValueSerializer<TKey, TValue> _keyValueSerializer;

        /// <summary>
        /// Prefix added to the keys of the <see cref="_cache"/>.
        /// </summary>
        private readonly string? _keyPrefix;

        public DistributedCache(string name, IAsyncCache cache, IKeyValueSerializer<TKey, TValue> keyValueSerializer,
            string? keyPrefix)
        {
            _name = name;
            _cache = cache;
            _keyValueSerializer = keyValueSerializer;
            _keyPrefix = keyPrefix;
        }

        /// <inheritdoc />
        public async Task<(AsyncCacheStatus status, CacheEntry<TValue?>? entry)> GetAsync(TKey key)
        {
            string keyStr = BuildDistributedCacheKey(key);
            AsyncCacheStatus status;
            byte[]? bytes;
            try
            {
                (status, bytes) = await _cache.GetAsync(keyStr);
            }
            catch
            {
                (status, bytes) = (AsyncCacheStatus.Error, null);
            }

            return status != AsyncCacheStatus.Hit
                ? (status, null)
                : (status, DeserializeDistributedCacheValue(bytes!));
        }

        /// <inheritdoc />
        public Task SetAsync(TKey key, CacheEntry<TValue?> entry)
        {
            string keyStr = BuildDistributedCacheKey(key);
            byte[] valueBytes = SerializeDistributedCacheValue(entry);
            TimeSpan timeToLive = entry.ExpirationTime - DateTime.UtcNow;
            return _cache.SetAsync(keyStr, valueBytes, timeToLive);
        }

        /// <summary>{prefix}|{cacheName}|{version}|{key}</summary>
        private string BuildDistributedCacheKey(TKey key)
        {
            string prefix = !string.IsNullOrEmpty(_keyPrefix)
                ? _keyPrefix + '|'
                : string.Empty;
            return prefix + _name
                          + '|' + _keyValueSerializer!.Version.ToString()
                          + '|' + _keyValueSerializer!.StringifyKey(key);
        }

        private byte[] SerializeDistributedCacheValue(CacheEntry<TValue?> entry)
        {
            using MemoryStream memoryStream = new();
            using (BinaryWriter writer = new(memoryStream))
            {
                writer.Write((byte)0); // Version, to add extra fields later.

                long unixExpirationTime = new DateTimeOffset(entry.ExpirationTime).ToUnixTimeMilliseconds();
                writer.Write(unixExpirationTime);

                _keyValueSerializer!.SerializeValue(entry.Value, writer);
            }

            return memoryStream.ToArray();
        }

        private CacheEntry<TValue?> DeserializeDistributedCacheValue(byte[] bytes)
        {
            byte version = bytes[0];

            long unixExpirationTime = BitConverter.ToInt64(bytes.AsSpan(sizeof(byte)));
            DateTime expirationTime = DateTimeOffset.FromUnixTimeMilliseconds(unixExpirationTime).UtcDateTime;

            TValue? value = _keyValueSerializer!.DeserializeValue(bytes.AsSpan(sizeof(byte) + sizeof(long)));

            return new CacheEntry<TValue?>(value, expirationTime);
        }
    }
}
