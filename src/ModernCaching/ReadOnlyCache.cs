using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.Instrumentation;
using ModernCaching.LocalCaching;
using ModernCaching.Utils;

namespace ModernCaching
{
    /// <inheritdoc />
    internal class ReadOnlyCache<TKey, TValue> : IReadOnlyCache<TKey, TValue> where TKey : IEquatable<TKey>
    {
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
        /// Library metrics.
        /// </summary>
        private readonly ICacheMetrics _metrics;

        /// <summary>
        /// Batch of keys to load in the background. The boolean value is not used.
        /// </summary>
        private ConcurrentDictionary<TKey, bool> _keysToLoad;

        /// <summary>
        /// Timer to load the keys from <see cref="_keysToLoad"/>.
        /// </summary>
        private readonly ITimer _backgroundLoadTimer;

        /// <summary>
        /// Random number generator to randomize time-to-lives.
        /// </summary>
        private readonly IRandom _random;

        /// <summary>
        /// To avoid loading the same key concurrently, the loading tasks are saved here to be reused by concurrent <see cref="TryGetAsync"/>.
        /// </summary>
        private readonly ConcurrentDictionary<TKey, Task<(bool found, TValue? value)>> _loadingTasks;

        public ReadOnlyCache(ICache<TKey, TValue>? localCache, IDistributedCache<TKey, TValue>? distributedCache,
            IDataSource<TKey, TValue> dataSource, ICacheMetrics metrics, ITimer loadingTimer, IRandom random)
        {
            _localCache = localCache;
            _distributedCache = distributedCache;
            _dataSource = dataSource;
            _metrics = metrics;
            _keysToLoad = ConcurrentDictionaryHelper.Create<TKey, bool>();
            _loadingTasks = ConcurrentDictionaryHelper.Create<TKey, Task<(bool, TValue?)>>();
            _backgroundLoadTimer = loadingTimer;
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
            TaskCompletionSource<(bool, TValue?)> reloadTaskCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
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

            var keysNotFoundInSource = keysToLoadFromSource.ToHashSet();

            var cancellationToken = CancellationToken.None; // TODO: what cancellation token should be passed to the loader?
            var results = _dataSource.LoadAsync(keysToLoadFromSource, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

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
                    keysNotFoundInSource.Remove(dataSourceResult.Key);
                }
                catch
                {
                    continue;
                }

                DateTime expirationTime = DateTime.UtcNow + RandomizeTimeSpan(dataSourceResult.TimeToLive);
                CacheEntry<TValue?> cacheEntry = new(dataSourceResult.Value, expirationTime);
                _ = Task.Run(() => SetRemotelyAsync(dataSourceResult.Key, cacheEntry));
                SetLocally(dataSourceResult.Key, cacheEntry);
            }

            await results.DisposeAsync();

            _metrics.IncrementDataSourceLoadHits(keysToLoadFromSource.Count - keysNotFoundInSource.Count);
            _metrics.IncrementDataSourceLoadMisses(keysNotFoundInSource.Count);

            // If the key was not found in the data source, it means that maybe it never existed or that it was
            // removed recently. For that second case, we should remove the potential cached value.
            // TODO: add an option to set to null instead of removing the entry?
            foreach (var key in keysNotFoundInSource)
            {
                RemoveLocally(key);
            }
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

        /// <summary>Sets the specified key and entry to the local cache.</summary>
        private void RemoveLocally(TKey key)
        {
            if (_localCache == null)
            {
                return;
            }

            _localCache.Remove(key);
        }

        /// <summary>Reloads the keys set in <see cref="_keysToLoad"/> by <see cref="TryPeek"/>.</summary>
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

            (bool loadSuccessful, var dataSourceEntry) = await LoadFromDataSourceAsync(key);
            if (!loadSuccessful)
            {
                // If the data source is unavailable return the stale value of the distributed cache or the stale
                // value of the local cache or not found.
                var availableCacheEntry = distributedCacheEntry ?? localCacheEntry;
                return (availableCacheEntry != null ? (true, availableCacheEntry.Value) : (false, default))!;
            }

            if (dataSourceEntry == null) // If no results were returned from the data source.
            {
                if (localCacheEntry != null)
                {
                    // The entry was recently removed from the data source so it should also be removed from the
                    // local cache. The entry in the distributed cache is not removed as it is supposed to be
                    // stale so it won't be used by this library and should get evicted soon.
                    RemoveLocally(key);
                }

                return (false, default)!;
            }

            _ = Task.Run(() => SetRemotelyAsync(key, dataSourceEntry));
            SetLocally(key, dataSourceEntry);
            return (true, dataSourceEntry.Value);
        }

        /// <summary>Checks if a <see cref="CacheEntry{TValue}"/> is stale.</summary>
        private bool IsCacheEntryStale(CacheEntry<TValue?> entry)
        {
            return entry.ExpirationTime < DateTime.UtcNow;
        }

        private Task<(AsyncCacheStatus status, CacheEntry<TValue?>? cacheEntry)> TryGetRemotelyAsync(TKey key)
        {
            if (_distributedCache == null)
            {
                // If there is no L2, consider it as a miss.
                return Task.FromResult((AsyncCacheStatus.Miss, null as CacheEntry<TValue?>));
            }

            return _distributedCache.GetAsync(key);
        }

        private Task SetRemotelyAsync(TKey key, CacheEntry<TValue?> entry)
        {
            if (_distributedCache == null)
            {
                return Task.CompletedTask;
            }

            return _distributedCache.SetAsync(key, entry);
        }

        private async Task<(bool success, CacheEntry<TValue?>? cacheEntry)> LoadFromDataSourceAsync(TKey key)
        {
            try
            {
                var cancellationToken = CancellationToken.None; // TODO: what cancellation token should be passed to the loader?
                await using var results = _dataSource.LoadAsync(new[] { key }, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
                if (!await results.MoveNextAsync())
                {
                    _metrics.IncrementDataSourceLoadMisses();
                    return (true, null);
                }

                _metrics.IncrementDataSourceLoadHits();
                var result = results.Current;
                DateTime expirationTime = DateTime.UtcNow + RandomizeTimeSpan(result.TimeToLive);
                return (true, new CacheEntry<TValue?>(result.Value, expirationTime));
            }
            catch
            {
                return (false, null);
            }
        }

        private TimeSpan RandomizeTimeSpan(TimeSpan ts)
        {
            double seconds = ts.TotalSeconds;
            seconds -= _random.Next(0, (int)(0.15 * seconds));
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
