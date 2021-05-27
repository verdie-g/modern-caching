using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;
using ModernCaching.Utils;

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
        /// Serializer for the <see cref="_distributedCache"/>. Should be set if <see cref="_distributedCache"/> is set.
        /// </summary>
        private readonly IKeyValueSerializer<TKey, TValue>? _keyValueSerializer;

        /// <summary>
        /// Prefix added to the keys of the <see cref="_distributedCache"/>.
        /// </summary>
        private readonly string? _distributedCacheKeyPrefix;

        /// <summary>
        /// Source of the data that is being cached.
        /// </summary>
        private readonly IDataSource<TKey, TValue> _dataSource;

        /// <summary>
        /// Batch of keys to load in the background. The boolean value is not used.
        /// </summary>
        private ConcurrentDictionary<TKey, bool> _keysToLoad;

        /// <summary>
        /// Timer to load the keys from <see cref="_keysToLoad"/>.
        /// </summary>
        private readonly ITimer _backgroundLoadTimer;

        /// <summary>
        /// To avoid loading the same key concurrently, the loading tasks are saved here to be reused by concurrent <see cref="TryGetAsync"/>.
        /// </summary>
        private readonly ConcurrentDictionary<TKey, Task<(bool found, TValue? value)>> _loadingTasks;

        public ReadOnlyCache(string name, ICache<TKey, TValue>? localCache, IAsyncCache? distributedCache,
            IKeyValueSerializer<TKey, TValue>? keyValueSerializer, string? distributedCacheKeyPrefix,
            IDataSource<TKey, TValue> dataSource, ITimer loadingTimer)
        {
            _name = name;
            _localCache = localCache;
            _distributedCache = distributedCache;
            _keyValueSerializer = keyValueSerializer;
            _distributedCacheKeyPrefix = distributedCacheKeyPrefix;
            _dataSource = dataSource;
            _keysToLoad = new ConcurrentDictionary<TKey, bool>();
            _loadingTasks = new ConcurrentDictionary<TKey, Task<(bool, TValue?)>>();
            _backgroundLoadTimer = loadingTimer;

            if (_distributedCache != null && _keyValueSerializer == null)
            {
                throw new ArgumentNullException(nameof(keyValueSerializer), "A serializer should be specified if a distributed cache is used");
            }

            _backgroundLoadTimer.Elapsed += BackgroundLoad;
        }

        /// <inheritdoc />
        public bool TryGet(TKey key, out TValue? value)
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
            value = found ? cacheEntry!.Value : default;
            return found;
        }

        /// <inheritdoc />
        public async ValueTask<(bool found, TValue? value)> TryGetAsync(TKey key)
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
            TaskCompletionSource<(bool, TValue?)> reloadTaskCompletion = new();
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

        public async Task LoadAsync(IEnumerable<TKey> keys)
        {
            // TODO: could also set tasks in _loadingTasks.

            var distributedCacheResults = await Task.WhenAll(keys.Select(async key =>
            {
                var (status, cacheEntry) = await TryGetRemotelyAsync(key);
                return (status, key, cacheEntry);
            }));

            var keysToLoadFromSource = new List<TKey>();
            foreach (var (status, key, cacheEntry) in distributedCacheResults)
            {
                // Filter out errors. If the distributed cache is not available, no reload is performed.
                if (status == AsyncCacheStatus.Error)
                {
                    continue;
                }

                if (status == AsyncCacheStatus.Hit)
                {
                    if (!IsCacheEntryStale(cacheEntry!))
                    {
                        SetLocally(key, cacheEntry!); // TODO: extend cacheEntry if != null.
                        continue;
                    }
                }

                keysToLoadFromSource.Add(key);
            }

            var cancellationToken = CancellationToken.None; // TODO: what cancellation token should be passed to the loader?
            IAsyncEnumerator<DataSourceResult<TKey, TValue?>> results;
            try
            {
                results = _dataSource
                    .LoadAsync(keysToLoadFromSource, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
            }
            catch
            {
                return;
            }

            while (true)
            {
                DataSourceResult<TKey, TValue?> dataSourceResult;
                try
                {
                    if (!await results.MoveNextAsync())
                    {
                        break;
                    }

                    dataSourceResult = results.Current;
                }
                catch
                {
                    continue;
                }

                CacheEntry<TValue?> cacheEntry = new(dataSourceResult.Value, DateTime.UtcNow + dataSourceResult.TimeToLive);
                _ = Task.Run(() => SetRemotelyAsync(dataSourceResult.Key, cacheEntry));
                SetLocally(dataSourceResult.Key, cacheEntry);
            }

            await results.DisposeAsync();
        }

        public void Dispose()
        {
            _backgroundLoadTimer.Elapsed -= BackgroundLoad;
        }

        /// <summary>Gets the value associated with the specified key from the local cache.</summary>
        private bool TryGetLocally(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue?> cacheEntry)
        {
            if (_localCache == null)
            {
                cacheEntry = null;
                return false;
            }

            return _localCache.TryGet(key, out cacheEntry);
        }

        /// <summary>Sets the specified key and entry to the local cache.</summary>
        private void SetLocally(TKey key, CacheEntry<TValue?> cacheEntry)
        {
            if (_localCache == null)
            {
                return;
            }

            _localCache.Set(key, cacheEntry);
        }

        /// <summary>Reloads the keys set in <see cref="_keysToLoad"/> by <see cref="TryGet"/>.</summary>
        private void BackgroundLoad(object _, ElapsedEventArgs __)
        {
            ICollection<TKey> keys = Interlocked.Exchange(ref _keysToLoad, new ConcurrentDictionary<TKey, bool>()).Keys;
            Task.Run(() => LoadAsync(keys));
        }

        /// <summary>
        /// Reloads a key from the distributed cache or data source. Can return a stale value if of these two layers
        /// are unavailable.
        /// </summary>
        private async Task<(bool found, TValue? value)> ReloadAsync(TKey key, CacheEntry<TValue?>? localCacheEntry)
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
                if (!IsCacheEntryStale(distributedCacheEntry!))
                {
                    SetLocally(key, distributedCacheEntry!); // TODO: extend cacheEntry if != null.
                    return (true, distributedCacheEntry!.Value);
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
        private async Task<(AsyncCacheStatus status, CacheEntry<TValue?>? cacheEntry)> TryGetRemotelyAsync(TKey key)
        {
            if (_distributedCache == null)
            {
                // If there is no L2, consider it as a miss.
                return (AsyncCacheStatus.Miss, null);
            }

            string keyStr = BuildDistributedCacheKey(key);
            AsyncCacheStatus status;
            byte[]? bytes;
            try
            {
                (status, bytes) = await _distributedCache.GetAsync(keyStr);
            }
            catch
            {
                (status, bytes) = (AsyncCacheStatus.Error, null);
            }

            return status != AsyncCacheStatus.Hit
                ? (status, null)
                : (status, DeserializeDistributedCacheValue(bytes!));
        }

        /// <summary>Sets the specified key and entry to the distributed cache.</summary>
        private Task SetRemotelyAsync(TKey key, CacheEntry<TValue?> cacheEntry)
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
        private bool IsCacheEntryStale(CacheEntry<TValue?> value)
        {
            return value.ExpirationTime < DateTime.UtcNow;
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

        private byte[] SerializeDistributedCacheValue(CacheEntry<TValue?> cacheEntry)
        {
            MemoryStream memoryStream = new();
            BinaryWriter writer = new(memoryStream);

            writer.Write((byte)0); // Version, to add extra fields later.

            long unixExpirationTime = new DateTimeOffset(cacheEntry.ExpirationTime).ToUnixTimeMilliseconds();
            writer.Write(unixExpirationTime);

            _keyValueSerializer!.SerializeValue(cacheEntry.Value, writer);

            return memoryStream.GetBuffer();
        }

        private CacheEntry<TValue?> DeserializeDistributedCacheValue(byte[] bytes)
        {
            byte version = bytes[0];

            long unixExpirationTime = BitConverter.ToInt64(bytes.AsSpan(sizeof(byte)));
            DateTime expirationTime = DateTimeOffset.FromUnixTimeMilliseconds(unixExpirationTime).UtcDateTime;

            TValue? value = _keyValueSerializer!.DeserializeValue(bytes.AsSpan(sizeof(byte) + sizeof(long)));

            return new CacheEntry<TValue?>(value, expirationTime);
        }

        private async Task<(bool success, CacheEntry<TValue?>? cacheEntry)> LoadFromDataSourceAsync(TKey key)
        {
            try
            {
                var cancellationToken = CancellationToken.None; // TODO: what cancellation token should be passed to the loader?
                await using var results = _dataSource
                    .LoadAsync(new[] { key }, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
                if (!await results.MoveNextAsync())
                {
                    return (true, null);
                }

                var result = results.Current;
                DateTime expirationTime = DateTime.UtcNow + result.TimeToLive;
                return (true, new CacheEntry<TValue?>(result.Value, expirationTime));
            }
            catch
            {
                return (false, null);
            }
        }
    }
}
