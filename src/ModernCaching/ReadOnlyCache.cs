using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    [DebuggerDisplay("{_name} (Count = {_localCache?.Count ?? 0})")]
    internal class ReadOnlyCache<TKey, TValue> : IReadOnlyCache<TKey, TValue> where TKey : notnull
    {
        /// <summary>
        /// Name of the cache.
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// First caching layer, local to the program. If null, this layer is always skipped.
        /// </summary>
        private readonly ICache<TKey, TValue>? _localCache;

        /// <summary>
        /// Second caching layer that is accessible from all instances of the program. If null this layer is always skipped.
        /// </summary>
        private readonly IDistributedCache<TKey, TValue>? _distributedCache;

        /// <summary>
        /// Source of the data that is being cached.
        /// </summary>
        private readonly IDataSource<TKey, TValue> _dataSource;

        /// <summary>
        /// Cache options.
        /// </summary>
        private readonly ReadOnlyCacheOptions _options;

        /// <summary>
        /// Batch of keys to load in the background.
        /// </summary>
        private ConcurrentDictionary<TKey, CacheEntry<TValue>?> _keysToLoad;

        /// <summary>
        /// Timer to load the keys from <see cref="_keysToLoad"/>.
        /// </summary>
        private readonly ITimer _backgroundLoadTimer;

        /// <summary>
        /// DateTime abstract to be able to mock time.
        /// </summary>
        private readonly IDateTime _dateTime;

        /// <summary>
        /// Random number generator to randomize time-to-lives.
        /// </summary>
        private readonly IRandom _random;

        /// <summary>
        /// To avoid loading the same key concurrently, the loading tasks are saved here to be reused by concurrent <see cref="TryGetAsync"/>.
        /// </summary>
        private readonly ConcurrentDictionary<TKey, Task<(bool found, TValue? value)>> _loadingTasks;

        public ReadOnlyCache(string name, ICache<TKey, TValue>? localCache,
            IDistributedCache<TKey, TValue>? distributedCache, IDataSource<TKey, TValue> dataSource,
            ReadOnlyCacheOptions options, ITimer loadingTimer, IDateTime dateTime, IRandom random)
        {
            _name = name;
            _localCache = localCache;
            _distributedCache = distributedCache;
            _dataSource = dataSource;
            _options = options;
            _keysToLoad = ConcurrentDictionaryHelper.Create<TKey, CacheEntry<TValue>?>();
            _loadingTasks = ConcurrentDictionaryHelper.Create<TKey, Task<(bool, TValue?)>>();
            _backgroundLoadTimer = loadingTimer;
            _dateTime = dateTime;
            _random = random;

            _backgroundLoadTimer.Elapsed += BackgroundLoad;
        }

        /// <inheritdoc />
        public bool TryPeek(TKey key, out TValue? value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            bool found = TryGetLocally(key, out var cacheEntry);
            if (found && !IsCacheEntryStale(cacheEntry!))
            {
                if (cacheEntry!.HasValue)
                {
                    value = cacheEntry.Value;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }

            // Not found or stale.
            _keysToLoad[key] = cacheEntry;
            value = found && cacheEntry!.HasValue ? cacheEntry.Value : default;
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
                return localCacheEntry.HasValue ? (true, localCacheEntry.Value) : (false, default);
            }

            // Multiplex concurrent reload of the same key into a single task.
            TaskCompletionSource<(bool, TValue?)> reloadTaskCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var reloadTask = _loadingTasks.GetOrAdd(key, reloadTaskCompletion.Task);
            if (reloadTask != reloadTaskCompletion.Task)
            {
                // The key is already being loaded.
                return await reloadTask;
            }

            try
            {
                var reloadResult = await ReloadAsync(key, localCacheEntry);
                reloadTaskCompletion.SetResult(reloadResult);
                return reloadResult;
            }
            catch (Exception e) // ReloadAsync shouldn't throw but the consequence of not removing the loading task is terrible.
            {
                reloadTaskCompletion.SetException(e);
                throw;
            }
            finally
            {
                _loadingTasks.Remove(key, out _);
            }
        }

        public Task LoadAsync(IEnumerable<TKey> keys)
        {
            return InnerLoadAsync(keys.Select(static k => new KeyValuePair<TKey, CacheEntry<TValue>?>(k, null)));
        }

        public override string ToString()
        {
            return _name;
        }

        public void Dispose()
        {
            _backgroundLoadTimer.Elapsed -= BackgroundLoad;
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

        /// <summary>Sets the specified key and entry to the local cache or extends the lifetime of an existing entry.</summary>
        private void SetOrExtendLocally(TKey key, CacheEntry<TValue> newCacheEntry, CacheEntry<TValue>? oldCacheEntry)
        {
            if (_localCache == null)
            {
                return;
            }

            // If an entry already exists for the key with the same value, extends its lifetime instead of replacing it
            // to avoid replacing a gen 2 object by gen 0 one which would induce gen 2 fragmentation.
            if (oldCacheEntry != null && CacheEntryEquals(oldCacheEntry, newCacheEntry))
            {
                oldCacheEntry.ExpirationTime = newCacheEntry.ExpirationTime;
                oldCacheEntry.EvictionTime = newCacheEntry.EvictionTime;
            }
            else
            {
                _localCache.Set(key, newCacheEntry);
            }
        }

        /// <summary>Sets the specified key and entry to the local cache.</summary>
        private void DeleteLocally(TKey key)
        {
            if (_localCache == null)
            {
                return;
            }

            _localCache.Delete(key);
        }

        /// <summary>Reloads the keys set in <see cref="_keysToLoad"/> by <see cref="TryPeek"/>.</summary>
        private void BackgroundLoad(object _, ElapsedEventArgs __)
        {
            var keysToLoad = Interlocked.Exchange(ref _keysToLoad, new ConcurrentDictionary<TKey, CacheEntry<TValue>?>());
            Task.Run(() => InnerLoadAsync(keysToLoad));
        }

        private async Task InnerLoadAsync(IEnumerable<KeyValuePair<TKey, CacheEntry<TValue>?>> keyEntryPairs)
        {
            // TODO: could also set tasks in _loadingTasks.

            var distributedCacheResults = await Task.WhenAll(keyEntryPairs.Select(async keyEntryPair =>
            {
                var (status, distributedCacheEntry) = await TryGetRemotelyAsync(keyEntryPair.Key);
                return (status, keyEntryPair, distributedCacheEntry);
            }));

            var keysToLoadFromSource = new List<KeyValuePair<TKey, CacheEntry<TValue>?>>(distributedCacheResults.Length);
            foreach (var (status, keyEntryPair, distributedCacheEntry) in distributedCacheResults)
            {
                // Filter out errors. If the distributed cache is not available, no reload is performed.
                if (status == AsyncCacheStatus.Error)
                {
                    continue;
                }

                if (status == AsyncCacheStatus.Hit)
                {
                    if (!IsCacheEntryStale(distributedCacheEntry!))
                    {
                        SetOrExtendLocally(keyEntryPair.Key, distributedCacheEntry!, keyEntryPair.Value);
                        continue;
                    }
                }

                keysToLoadFromSource.Add(keyEntryPair);
            }

            var localCacheEntriesByKey = keysToLoadFromSource.ToDictionary(k => k.Key, k => k.Value);

            var cancellationToken = CancellationToken.None; // TODO: what cancellation token should be passed to the loader?
            await foreach (var dataSourceResult in _dataSource.LoadAsync(keysToLoadFromSource.Select(k => k.Key), cancellationToken))
            {
                CacheEntry<TValue>? localCacheEntry = localCacheEntriesByKey[dataSourceResult.Key];
                localCacheEntriesByKey.Remove(dataSourceResult.Key);

                CacheEntry<TValue> dataSourceCacheEntry = CacheEntryFromDataSourceResult(dataSourceResult);
                _ = Task.Run(() => SetRemotelyAsync(dataSourceResult.Key, dataSourceCacheEntry));
                SetOrExtendLocally(dataSourceResult.Key, dataSourceCacheEntry, localCacheEntry);
            }

            if (_options.CacheDataSourceMisses)
            {
                TimeSpan ttl = _options.DefaultTimeToLive;
                foreach (var (key, _) in localCacheEntriesByKey)
                {
                    CacheEntry<TValue> dataSourceCacheEntry = InitCacheEntryTimeToLive(new CacheEntry<TValue>(), ttl);
                    _ = Task.Run(() => SetRemotelyAsync(key, dataSourceCacheEntry));
                    SetOrExtendLocally(key, dataSourceCacheEntry, null);
                }
            }
            else
            {
                // If the key was not found in the data source, it means that maybe it never existed or that it was
                // deleted recently. For that second case, we should delete the potential cached value.
                foreach (var (key, _) in localCacheEntriesByKey)
                {
                    _ = Task.Run(() => DeleteRemotelyAsync(key));
                    DeleteLocally(key);
                }
            }
        }

        /// <summary>
        /// Reloads a key from the distributed cache or data source. Can return a stale value if of these two layers
        /// are unavailable.
        /// </summary>
        private async Task<(bool found, TValue? value)> ReloadAsync(TKey key, CacheEntry<TValue>? localCacheEntry)
        {
            var (status, distributedCacheEntry) = await TryGetRemotelyAsync(key);
            if (status == AsyncCacheStatus.Error)
            {
                // If the distributed cache is unavailable, to avoid DDOSing the data source, return the stale
                // local entry or not found.
                return localCacheEntry != null && localCacheEntry.HasValue
                    ? (true, localCacheEntry.Value)
                    : (false, default);
            }

            if (status == AsyncCacheStatus.Hit)
            {
                if (!IsCacheEntryStale(distributedCacheEntry!))
                {
                    SetOrExtendLocally(key, distributedCacheEntry!, localCacheEntry);
                    return distributedCacheEntry!.HasValue ? (true, distributedCacheEntry.Value) : (false, default);
                }
            }

            (bool loadSuccessful, var dataSourceEntry) = await LoadFromDataSourceAsync(key);
            if (!loadSuccessful)
            {
                // If the data source is unavailable return the stale value of the distributed cache or the stale
                // value of the local cache or not found.
                var availableCacheEntry = distributedCacheEntry ?? localCacheEntry;
                return availableCacheEntry != null && availableCacheEntry.HasValue
                    ? (true, availableCacheEntry.Value)
                    : (false, default);
            }

            if (dataSourceEntry == null) // If no results were returned from the data source.
            {
                if (localCacheEntry != null)
                {
                    // The entry was recently deleted from the data source so it should also be deleted from the
                    // local and distributed cache.
                    _ = Task.Run(() => DeleteRemotelyAsync(key));
                    DeleteLocally(key);
                }

                return (false, default);
            }

            _ = Task.Run(() => SetRemotelyAsync(key, dataSourceEntry));
            SetOrExtendLocally(key, dataSourceEntry, localCacheEntry);
            return dataSourceEntry.HasValue ? (true, dataSourceEntry.Value) : (false, default);
        }

        /// <summary>Checks if a <see cref="CacheEntry{TValue}"/> is stale.</summary>
        private bool IsCacheEntryStale(CacheEntry<TValue> entry)
        {
            return entry.ExpirationTime < _dateTime.UtcNow;
        }

        private Task<(AsyncCacheStatus status, CacheEntry<TValue>? cacheEntry)> TryGetRemotelyAsync(TKey key)
        {
            if (_distributedCache == null)
            {
                // If there is no L2, consider it as a miss.
                return Task.FromResult((AsyncCacheStatus.Miss, null as CacheEntry<TValue>));
            }

            return _distributedCache.GetAsync(key);
        }

        private Task SetRemotelyAsync(TKey key, CacheEntry<TValue> entry)
        {
            if (_distributedCache == null)
            {
                return Task.CompletedTask;
            }

            return _distributedCache.SetAsync(key, entry);
        }

        private Task DeleteRemotelyAsync(TKey key)
        {
            if (_distributedCache == null)
            {
                return Task.CompletedTask;
            }

            return _distributedCache.DeleteAsync(key);
        }

        private async Task<(bool success, CacheEntry<TValue>? cacheEntry)> LoadFromDataSourceAsync(TKey key)
        {
            try
            {
                var cancellationToken = CancellationToken.None; // TODO: what cancellation token should be passed to the loader?
                await using var results = _dataSource.LoadAsync(new[] { key }, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
                if (!await results.MoveNextAsync())
                {
                    return _options.CacheDataSourceMisses
                        ? (true, InitCacheEntryTimeToLive(new CacheEntry<TValue>(), _options.DefaultTimeToLive))
                        : (true, null);
                }

                return (true, CacheEntryFromDataSourceResult(results.Current));
            }
            catch
            {
                return (false, null);
            }
        }

        private bool CacheEntryEquals(CacheEntry<TValue> cacheEntry1, CacheEntry<TValue> cacheEntry2)
        {
            if (cacheEntry1.HasValue)
            {
                // Equals will involve boxing for structs that don't implement IEquatable.
                return cacheEntry2.HasValue
                       && EqualityComparer<TValue>.Default.Equals(cacheEntry1.Value, cacheEntry2.Value);
            }

            return !cacheEntry2.HasValue;
        }

        private CacheEntry<TValue> CacheEntryFromDataSourceResult(DataSourceResult<TKey, TValue> result)
        {
            return InitCacheEntryTimeToLive(new CacheEntry<TValue>(result.Value), result.TimeToLive);
        }

        private CacheEntry<TValue> InitCacheEntryTimeToLive(CacheEntry<TValue> entry, TimeSpan timeToLive)
        {
            TimeSpan randomizedTimeToLive = RandomizeTimeSpan(timeToLive);
            DateTime utcNow = _dateTime.UtcNow;

            entry.ExpirationTime = utcNow + randomizedTimeToLive;
            entry.EvictionTime = utcNow + randomizedTimeToLive * 2; // Entries are kept in cache twice longer than the expiration time.

            return entry;
        }

        private TimeSpan RandomizeTimeSpan(TimeSpan ts)
        {
            double seconds = ts.TotalSeconds;
            seconds -= _random.Next(0, (int)(0.15 * seconds));
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
