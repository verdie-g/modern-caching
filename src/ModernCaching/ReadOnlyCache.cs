using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;

namespace ModernCaching
{
    /// <inheritdoc />
    internal class ReadOnlyCache<TKey, TValue> : IReadOnlyCache<TKey, TValue> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Name of cache. Used in the distributed cache key, logging and metrics.
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// First caching layer, local to the program. If null, this layer is always skipped.
        /// </summary>
        private readonly ICache<TKey, TValue>? _localCache;

        /// <summary>
        /// Second caching layer that is accessible from all instances of the program. If null this layer is always skipped.
        /// </summary>
        private readonly IAsyncCache? _distributedCache;

        /// <summary>
        /// Source of the data that is being cached.
        /// </summary>
        private readonly IDataSource<TKey, TValue> _dataSource;

        /// <summary>
        /// Serializer for the <see cref="_distributedCache"/>. Should be set if <see cref="_distributedCache"/> is set.
        /// </summary>
        private readonly IKeyValueSerializer<TKey, TValue>? _keyValueSerializer;

        /// <summary>
        /// Prefix added to the keys of the <see cref="_distributedCache"/>.
        /// </summary>
        private readonly string? _distributedCacheKeyPrefix;

        /// <summary>
        /// Batch of keys to load in the background. The boolean value is not used.
        /// </summary>
        private ConcurrentDictionary<TKey, bool> _keysToLoad;

        /// <summary>
        /// Timer to load the keys from <see cref="_keysToLoad"/>.
        /// </summary>
        private readonly Timer _backgroundLoadTimer;

        /// <summary>
        /// To avoid loading the same key concurrently, the loading tasks are saved here to be reused by concurrent <see cref="TryGetAsync"/>.
        /// </summary>
        private readonly ConcurrentDictionary<TKey, Task<(bool found, TValue value)>> _loadingTasks;

        public ReadOnlyCache(string name, ICache<TKey, TValue>? localCache, IAsyncCache? distributedCache,
            IDataSource<TKey, TValue> dataSource, IKeyValueSerializer<TKey, TValue>? keyValueSerializer,
            string? distributedCacheKeyPrefix)
        {
            _name = name;
            _localCache = localCache;
            _distributedCache = distributedCache;
            _dataSource = dataSource;
            _keyValueSerializer = keyValueSerializer;
            _distributedCacheKeyPrefix = distributedCacheKeyPrefix;
            _keysToLoad = new ConcurrentDictionary<TKey, bool>();
            _loadingTasks = new ConcurrentDictionary<TKey, Task<(bool, TValue)>>();
            _backgroundLoadTimer = new Timer(_ => Task.Run(BackgroundLoad), null, TimeSpan.FromSeconds(3),
                Timeout.InfiniteTimeSpan);

            if (_distributedCache != null)
            {
                if (_keyValueSerializer == null)
                {
                    throw new ArgumentNullException(nameof(keyValueSerializer), "A serializer should be specified if a distributed cache is used");
                }
            }
        }

        /// <inheritdoc />
        public bool TryGet(TKey key, out TValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            bool found = TryGetLocally(key, out var cacheEntry);
            if (found && !IsCacheEntryStale(cacheEntry!))
            {
                value = cacheEntry!.Value;
                return true;
            }

            // Not found or stale.
            _keysToLoad[key] = true;
            value = (found ? cacheEntry!.Value : default)!;
            return found;
        }

        /// <inheritdoc />
        public async ValueTask<(bool found, TValue value)> TryGetAsync(TKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (TryGetLocally(key, out var localCacheEntry) && !IsCacheEntryStale(localCacheEntry))
            {
                return (true, localCacheEntry.Value);
            }

            // Multiplex concurrent reload of the same key into a single task.
            TaskCompletionSource<(bool, TValue)> reloadTaskCompletion = new();
            var reloadTask = _loadingTasks.GetOrAdd(key, reloadTaskCompletion.Task);
            if (reloadTask != reloadTaskCompletion.Task)
            {
                // The key is already being loaded.
                return await reloadTask;
            }

            var reloadResult = await ReloadAsync(key, localCacheEntry);
            reloadTaskCompletion.SetResult(reloadResult);
            _loadingTasks.Remove(key, out _);
            return reloadResult;
        }

        /// <summary>Gets the value associated with the specified key from the local cache.</summary>
        private bool TryGetLocally(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue> cacheEntry)
        {
            if (_localCache == null)
            {
                cacheEntry = null;
                return false;
            }

            return _localCache.TryGet(key, out cacheEntry);
        }

        /// <summary>Sets the specified key and entry to the local cache.</summary>
        private void SetLocally(TKey key, CacheEntry<TValue> cacheEntry)
        {
            if (_localCache == null)
            {
                return;
            }

            _localCache.Set(key, cacheEntry);
        }

        /// <summary>Reloads the keys set in <see cref="_keysToLoad"/> by <see cref="TryGet"/>.</summary>
        private async Task BackgroundLoad()
        {
            // TODO: could also set tasks in _loadingTasks.

            ICollection<TKey> keys = Interlocked.Exchange(ref _keysToLoad, new ConcurrentDictionary<TKey, bool>()).Keys;
            var distributedCacheResults = await Task.WhenAll(keys.Select(async key =>
            {
                var (status, cacheEntry) = await TryGetRemotelyAsync(key);
                return (status, key, cacheEntry);
            }));

            keys = new List<TKey>();
            foreach (var (status, key, cacheEntry) in distributedCacheResults)
            {
                // Filter out errors. If the distributed cache is not available, no reload is performed.
                if (status == AsyncCacheStatus.Error)
                {
                    continue;
                }

                if (status == AsyncCacheStatus.Hit)
                {
                    if (cacheEntry!.ExpirationTime < DateTime.UtcNow)
                    {
                        SetLocally(key, cacheEntry); // TODO: extend cacheEntry if != null.
                        continue;
                    }
                }

                keys.Add(key);
            }

            var cancellationToken = CancellationToken.None; // TODO: what cancellation token should be passed to the loader?
            var resultsEnumerable = _dataSource.LoadAsync(keys, cancellationToken);
            await using var resultsEnumerator = resultsEnumerable.GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                LoadResult<TKey, TValue> loadResult;
                try
                {
                    if (!await resultsEnumerator.MoveNextAsync())
                    {
                        break;
                    }

                    loadResult = resultsEnumerator.Current;
                }
                catch
                {
                    continue;
                }

                CacheEntry<TValue> cacheEntry = new(loadResult.Value, DateTime.UtcNow + loadResult.TimeToLive);
                _ = Task.Run(() => SetRemotelyAsync(loadResult.Key, cacheEntry));
                SetLocally(loadResult.Key, cacheEntry);
            }

            _backgroundLoadTimer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Reloads a key from the distributed cache or data source. Can return a stale value if of these two layers
        /// are unavailable.
        /// </summary>
        private async Task<(bool found, TValue value)> ReloadAsync(TKey key, CacheEntry<TValue>? localCacheEntry)
        {
            var (status, distributedCacheEntry) = await TryGetRemotelyAsync(key);
            if (status == AsyncCacheStatus.Error)
            {
                // If the distributed cache is unavailable, to avoid DDOSing the data source, return the stale
                // local entry or not found.
                return (localCacheEntry != null ? (true, localCacheEntry.Value) : (false, default))!;
            }

            if (status == AsyncCacheStatus.Hit)
            {
                if (distributedCacheEntry!.ExpirationTime < DateTime.UtcNow)
                {
                    SetLocally(key, distributedCacheEntry); // TODO: extend cacheEntry if != null.
                    return (true, distributedCacheEntry.Value);
                }
            }

            var (loadSuccessful, dataSourceEntry) = await LoadFromDataSourceAsync(key);
            if (!loadSuccessful)
            {
                // If the data source is unavailable return the stale value of the distributed cache or the stale
                // value of the local cache or not found.
                var availableCacheEntry = distributedCacheEntry ?? localCacheEntry;
                return (availableCacheEntry != null ? (true, availableCacheEntry.Value) : (false, default))!;
            }

            if (dataSourceEntry == null) // If no results were returned from the data source.
            {
                return (false, default)!;
            }

            _ = Task.Run(() => SetRemotelyAsync(key, dataSourceEntry));
            SetLocally(key, dataSourceEntry);
            return (true, dataSourceEntry.Value);
        }

        /// <summary>Gets the value associated with the specified key from the distributed cache.</summary>
        private async Task<(AsyncCacheStatus status, CacheEntry<TValue>? cacheEntry)> TryGetRemotelyAsync(TKey key)
        {
            if (_distributedCache == null)
            {
                // If there is no L2, consider it as a miss.
                return (AsyncCacheStatus.Miss, null);
            }

            string keyStr = BuildDistributedCacheKey(key);
            var (status, bytes) = await _distributedCache.GetAsync(keyStr);
            return status != AsyncCacheStatus.Hit
                ? (status, null)
                : (status, DeserializeDistributedCacheValue(bytes));
        }

        /// <summary>Sets the specified key and entry to the distributed cache.</summary>
        private Task SetRemotelyAsync(TKey key, CacheEntry<TValue> cacheEntry)
        {
            if (_distributedCache == null)
            {
                return Task.CompletedTask;
            }

            string keyStr = BuildDistributedCacheKey(key);
            byte[] valueBytes = SerializeDistributedCacheValue(cacheEntry);
            TimeSpan timeToLive = cacheEntry.ExpirationTime - DateTime.UtcNow;
            return _distributedCache.SetAsync(keyStr, valueBytes, timeToLive);
        }

        /// <summary>Checks if a <see cref="CacheEntry{TValue}"/> is stale.</summary>
        private bool IsCacheEntryStale(CacheEntry<TValue> value)
        {
            return value.ExpirationTime > DateTime.Now;
        }

        /// <summary>{prefix}|{cacheName}|{version}|{key}</summary>
        private string BuildDistributedCacheKey(TKey key)
        {
            string prefix = !string.IsNullOrEmpty(_distributedCacheKeyPrefix)
                ? _distributedCacheKeyPrefix + '|'
                : string.Empty;
            return prefix + _name
                          + '|' + _keyValueSerializer!.Version.ToString()
                          + '|' + _keyValueSerializer!.StringifyKey(key);
        }

        private byte[] SerializeDistributedCacheValue(CacheEntry<TValue> cacheEntry)
        {
            MemoryStream memoryStream = new();
            BinaryWriter writer = new(memoryStream);

            writer.Write((byte)0); // Version, to add extra fields later.

            writer.Write(cacheEntry.ExpirationTime.Ticks);

            _keyValueSerializer!.SerializeValue(cacheEntry.Value, memoryStream);

            return memoryStream.GetBuffer();
        }

        private CacheEntry<TValue> DeserializeDistributedCacheValue(byte[] bytes)
        {
            byte version = bytes[0];

            long expirationTimeTicks = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(1));
            DateTime expirationTime = new(expirationTimeTicks, DateTimeKind.Utc);

            TValue value = _keyValueSerializer!.DeserializeValue(bytes.AsSpan(5));

            return new CacheEntry<TValue>(value, expirationTime);
        }

        private async Task<(bool success, CacheEntry<TValue>? cacheEntry)> LoadFromDataSourceAsync(TKey key)
        {
            try
            {
                var cancellationToken = CancellationToken.None; // TODO: what cancellation token should be passed to the loader?
                var resultsEnumerable = _dataSource.LoadAsync(new[] { key }, cancellationToken);
                await using var resultsEnumerator = resultsEnumerable.GetAsyncEnumerator(cancellationToken);
                if (!await resultsEnumerator.MoveNextAsync())
                {
                    return (true, null);
                }

                var result = resultsEnumerator.Current;
                DateTime expirationTime = DateTime.UtcNow + result.TimeToLive;
                return (true, new CacheEntry<TValue>(result.Value, expirationTime));
            }
            catch
            {
                return (false, null);
            }
        }
    }
}
