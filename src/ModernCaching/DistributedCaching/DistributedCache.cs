using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModernCaching.LocalCaching;
using ModernCaching.Utils;

namespace ModernCaching.DistributedCaching
{
    internal interface IDistributedCache<in TKey, TValue> where TKey : notnull
    {
        /// <summary>Gets the entry associated with the specified key from the distributed cache.</summary>
        Task<(AsyncCacheStatus status, CacheEntry<TValue>? entry)> GetAsync(TKey key);

        /// <summary>Sets the specified key and entry to the distributed cache.</summary>
        Task SetAsync(TKey key, CacheEntry<TValue> entry);

        /// <summary>Deletes the value with the given key from the distributed cache.</summary>
        Task DeleteAsync(TKey key);
    }

    /// <summary>
    /// Internal class that wraps a generic <see cref="IAsyncCache"/> with a <see cref="IKeyValueSerializer{TKey,TValue}"/>
    /// for a specific <see cref="ReadOnlyCache{TKey,TValue}"/>.
    /// </summary>
    internal class DistributedCache<TKey, TValue> : IDistributedCache<TKey, TValue> where TKey : notnull
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

        private readonly ILogger? _logger;

        public DistributedCache(string name, IAsyncCache cache, IKeyValueSerializer<TKey, TValue> keyValueSerializer,
            string? keyPrefix, ILogger? logger)
        {
            _name = name;
            _cache = cache;
            _keyValueSerializer = keyValueSerializer;
            _keyPrefix = keyPrefix;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<(AsyncCacheStatus status, CacheEntry<TValue>? entry)> GetAsync(TKey key)
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

            if (status != AsyncCacheStatus.Hit)
            {
                return (status, null);
            }

            try
            {
                return (status, DeserializeDistributedCacheValue(bytes!));
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "An error occured deserializing value for key '{key}' from cache '{cacheName}'", key, _name);
                return (AsyncCacheStatus.Error, null);
            }
        }

        /// <inheritdoc />
        public Task SetAsync(TKey key, CacheEntry<TValue> entry)
        {
            byte[] valueBytes;
            try
            {
                valueBytes = SerializeDistributedCacheValue(entry);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "An error occured serializing value for key '{key}' from cache '{cacheName}'", key, _name);
                return Task.CompletedTask;
            }

            string keyStr = BuildDistributedCacheKey(key);
            TimeSpan timeToLive = entry.EvictionTime - DateTime.UtcNow;
            return _cache.SetAsync(keyStr, valueBytes, timeToLive);
        }

        /// <inheritdoc />
        public Task DeleteAsync(TKey key)
        {
            string keyStr = BuildDistributedCacheKey(key);
            return _cache.DeleteAsync(keyStr);
        }

        /// <summary>{prefix}|{cacheName}|{version}|{key}</summary>
        private string BuildDistributedCacheKey(TKey key)
        {
            string prefix = !string.IsNullOrEmpty(_keyPrefix)
                ? _keyPrefix + '|'
                : string.Empty;
            return prefix + _name
                          + '|' + _keyValueSerializer.Version.ToString()
                          + '|' + _keyValueSerializer.StringifyKey(key);
        }

        private byte[] SerializeDistributedCacheValue(CacheEntry<TValue> entry)
        {
            MemoryStream memoryStream = UtilsCache.MemoryStreamPool.Get();
            BinaryWriter writer = new(memoryStream);

            writer.Write((byte)0); // Version, to add extra fields later.

            writer.Write((int)AsyncCacheEntryOptions.None);

            long unixExpirationTime = new DateTimeOffset(entry.ExpirationTime).ToUnixTimeSeconds();
            writer.Write(unixExpirationTime);

            long unixEvictionTime = new DateTimeOffset(entry.EvictionTime).ToUnixTimeSeconds();
            writer.Write(unixEvictionTime);

            _keyValueSerializer.SerializeValue(entry.Value, writer);

            byte[] bytes = memoryStream.ToArray();
            UtilsCache.MemoryStreamPool.Return(memoryStream);
            return bytes;
        }

        private CacheEntry<TValue> DeserializeDistributedCacheValue(byte[] bytes)
        {
            MemoryStream memoryStream = new(bytes, writable: false);
            BinaryReader reader = new(memoryStream);

            byte version = reader.ReadByte();

            var options = (AsyncCacheEntryOptions)reader.ReadInt32();

            long unixExpirationTime = reader.ReadInt64();
            DateTime expirationTime = DateTimeOffset.FromUnixTimeSeconds(unixExpirationTime).UtcDateTime;

            long unixEvictionTime = reader.ReadInt64();
            DateTime evictionTime = DateTimeOffset.FromUnixTimeSeconds(unixEvictionTime).UtcDateTime;

            TValue value = _keyValueSerializer.DeserializeValue(reader);

            return new CacheEntry<TValue>(value)
            {
                ExpirationTime = expirationTime,
                EvictionTime = evictionTime,
            };
        }
    }
}
