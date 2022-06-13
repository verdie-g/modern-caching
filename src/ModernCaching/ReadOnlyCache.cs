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
    internal sealed class ReadOnlyCache<TKey, TValue> : IReadOnlyCache<TKey, TValue> where TKey : notnull
    {
        /// <summary>
        /// Number max of keys sent to <see cref="IDataSource{TKey,TValue}.LoadAsync"/>.
        /// </summary>
        private const int LoadBatchSize = 1000;

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
        /// Batch of keys to refresh in the background.
        /// </summary>
        private ConcurrentDictionary<TKey, CacheEntry<TValue>?> _keysToRefresh;

        /// <summary>
        /// Timer to refresh the keys from <see cref="_keysToRefresh"/>.
        /// </summary>
        private readonly ITimer _backgroundRefreshTimer;

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
        private readonly ConcurrentDictionary<TKey, Task<CacheEntry<TValue>?>> _refreshTasks;

        public ReadOnlyCache(string name, ICache<TKey, TValue>? localCache,
            IDistributedCache<TKey, TValue>? distributedCache, IDataSource<TKey, TValue> dataSource,
            ReadOnlyCacheOptions options, ITimer loadingTimer, IDateTime dateTime, IRandom random)
        {
            _name = name;
            _localCache = localCache;
            _distributedCache = distributedCache;
            _dataSource = dataSource;
            _options = options;
            _keysToRefresh = ConcurrentDictionaryHelper.Create<TKey, CacheEntry<TValue>?>();
            _refreshTasks = ConcurrentDictionaryHelper.Create<TKey, Task<CacheEntry<TValue>?>>();
            _backgroundRefreshTimer = loadingTimer;
            _dateTime = dateTime;
            _random = random;

            _backgroundRefreshTimer.Elapsed += BackgroundRefresh;
        }

        /// <inheritdoc />
        public bool TryPeek(TKey key, [MaybeNullWhen(false)] out TValue value)
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
            _keysToRefresh[key] = cacheEntry;
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

            // Multiplex concurrent refresh of the same key into a single task.
            TaskCompletionSource<CacheEntry<TValue>?> refreshTaskCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshTask = _refreshTasks.GetOrAdd(key, refreshTaskCompletion.Task);
            if (refreshTask != refreshTaskCompletion.Task)
            {
                // The key is already being loaded.
                var refreshedCacheEntry = await refreshTask;
                return refreshedCacheEntry != null && refreshedCacheEntry.HasValue
                    ? (true, refreshedCacheEntry.Value)
                    : (false, default);
            }

            try
            {
                var refreshedCacheEntry = await RefreshAsync(key, localCacheEntry);
                refreshTaskCompletion.SetResult(refreshedCacheEntry);
                return refreshedCacheEntry != null && refreshedCacheEntry.HasValue
                    ? (true, refreshedCacheEntry.Value)
                    : (false, default);
            }
            catch (Exception e) // RefreshAsync shouldn't throw but the consequence of not removing the loading task is terrible.
            {
                refreshTaskCompletion.SetException(e);
                throw;
            }
            finally
            {
                _refreshTasks.Remove(key, out _);
            }
        }

        public Task LoadAsync(IEnumerable<TKey> keys)
        {
            return ChunkedRefreshAsync(keys.Select(static k => new KeyValuePair<TKey, CacheEntry<TValue>?>(k, null)));
        }

        public override string ToString()
        {
            return _name;
        }

        public void Dispose()
        {
            _backgroundRefreshTimer.Elapsed -= BackgroundRefresh;
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

            newCacheEntry.TimeToLive = RandomizeTimeSpan(newCacheEntry.TimeToLive);

            // If an entry already exists for the key with the same value, extends its lifetime instead of replacing it
            // to avoid replacing a gen 2 object by gen 0 one which would induce gen 2 fragmentation.
            if (newCacheEntry.Equals(oldCacheEntry))
            {
                oldCacheEntry.CreationTime = newCacheEntry.CreationTime;
                oldCacheEntry.TimeToLive = newCacheEntry.TimeToLive;
            }
            else
            {
                _localCache.Set(key, newCacheEntry);
            }
        }

        /// <summary>Deletes the entry with the specified key in the local cache.</summary>
        private bool TryDeleteLocally(TKey key)
        {
            if (_localCache == null)
            {
                return false;
            }

            return _localCache.TryDelete(key);
        }

        /// <summary>Refreshes the keys set in <see cref="_keysToRefresh"/> by <see cref="TryPeek"/>.</summary>
        private void BackgroundRefresh(object _, ElapsedEventArgs __)
        {
            var keysToLoad = Interlocked.Exchange(ref _keysToRefresh, new ConcurrentDictionary<TKey, CacheEntry<TValue>?>());
            Task.Run(() => ChunkedRefreshAsync(keysToLoad));
        }

        private async Task ChunkedRefreshAsync(IEnumerable<KeyValuePair<TKey, CacheEntry<TValue>?>> keyEntryPairs)
        {
            // If many instances start their preload at the same time they could all get misses/stale from the distributed
            // cache and all proceed to hit the data source which is dangerous. Also if the data source is a relational
            // database it's often not great to do a filter on a very large number of keys. To cope with these two problems
            // the load is done in several times by chunking the keys.
            foreach (var batch in ChunkKeyEntryPairs(keyEntryPairs))
            {
                await RefreshAsync(batch);
            }
        }

        private IEnumerable<KeyValuePair<TKey, CacheEntry<TValue>?>[]> ChunkKeyEntryPairs(
            IEnumerable<KeyValuePair<TKey, CacheEntry<TValue>?>> keyEntryPairs)
        {
            using var e = keyEntryPairs.GetEnumerator();
            while (e.MoveNext())
            {
                var chunk = new KeyValuePair<TKey, CacheEntry<TValue>?>[LoadBatchSize];
                chunk[0] = e.Current;

                int i = 1;
                for (; i < chunk.Length && e.MoveNext(); i += 1)
                {
                    chunk[i] = e.Current;
                }

                if (i == chunk.Length)
                {
                    yield return chunk;
                }
                else
                {
                    Array.Resize(ref chunk, i);
                    yield return chunk;
                }
            }
        }

        private async Task RefreshAsync(IEnumerable<KeyValuePair<TKey, CacheEntry<TValue>?>> keyEntryPairs)
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
                // Filter out errors. If the distributed cache is not available, no refresh is performed.
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

            if (keysToLoadFromSource.Count == 0)
            {
                return;
            }

            var localCacheEntriesByKey = keysToLoadFromSource.ToDictionary(k => k.Key, k => k.Value);

            using CancellationTokenSource cts = new(_options.LoadTimeout);
            await foreach (var dataSourceResult in _dataSource.LoadAsync(keysToLoadFromSource.Select(k => k.Key), cts.Token))
            {
                localCacheEntriesByKey.Remove(dataSourceResult.Key, out CacheEntry<TValue>? localCacheEntry);

                CacheEntry<TValue> dataSourceCacheEntry = NewCacheEntry(dataSourceResult.Value, dataSourceResult.TimeToLive);
                _ = Task.Run(() => SetRemotelyAsync(dataSourceResult.Key, dataSourceCacheEntry));
                // SetOrExtendLocally randomizes the TTL so to avoid SetRemotelyAsync to set a random TTL, the cache entry is cloned.
                SetOrExtendLocally(dataSourceResult.Key, dataSourceCacheEntry.Clone(), localCacheEntry);
            }

            if (_options.CacheDataSourceMisses)
            {
                TimeSpan ttl = _options.DefaultTimeToLive;
                foreach (var (key, _) in localCacheEntriesByKey)
                {
                    CacheEntry<TValue> dataSourceCacheEntry = NewCacheEntry(ttl);
                    _ = Task.Run(() => SetRemotelyAsync(key, dataSourceCacheEntry));
                    SetOrExtendLocally(key, dataSourceCacheEntry.Clone(), null);
                }
            }
            else
            {
                // If the key was not found in the data source, it means that maybe it never existed or that it was
                // deleted recently. For that second case, we should delete the potential cached value.
                foreach (var (key, _) in localCacheEntriesByKey)
                {
                    // Only delete the potential entry in the distributed cache if one existed in the local cache to
                    // avoid sending useless deletes everytime an entry was not found in the data source.
                    if (TryDeleteLocally(key))
                    {
                        _ = Task.Run(() => DeleteRemotelyAsync(key));
                    }
                }
            }
        }

        /// <summary>
        /// Refreshes a key from the distributed cache or data source. Can return a stale value if of these two layers
        /// are unavailable.
        /// </summary>
        private async Task<CacheEntry<TValue>?> RefreshAsync(TKey key, CacheEntry<TValue>? localCacheEntry)
        {
            var (status, distributedCacheEntry) = await TryGetRemotelyAsync(key);
            if (status == AsyncCacheStatus.Error)
            {
                // If the distributed cache is unavailable, to avoid DDOSing the data source, return the stale
                // local entry or not found.
                return localCacheEntry;
            }

            if (status == AsyncCacheStatus.Hit && !IsCacheEntryStale(distributedCacheEntry!))
            {
                SetOrExtendLocally(key, distributedCacheEntry!, localCacheEntry);
                return distributedCacheEntry;
            }

            (bool loadSuccessful, var dataSourceEntry) = await LoadFromDataSourceAsync(key);
            if (!loadSuccessful)
            {
                // If the data source is unavailable return the stale value of the distributed cache or the stale
                // value of the local cache or not found.
                return distributedCacheEntry ?? localCacheEntry;
            }

            if (dataSourceEntry == null) // If no results were returned from the data source.
            {
                if (_options.CacheDataSourceMisses)
                {
                     dataSourceEntry = NewCacheEntry(_options.DefaultTimeToLive);
                }
                else
                {
                    // If a local entry exists, the data was recently deleted from the source so it should also be
                    // deleted from the local and distributed cache.
                    if (localCacheEntry != null && TryDeleteLocally(key))
                    {
                        _ = Task.Run(() => DeleteRemotelyAsync(key));
                    }

                    return null;
                }
            }

            _ = Task.Run(() => SetRemotelyAsync(key, dataSourceEntry));
            // SetOrExtendLocally randomizes the TTL so to avoid SetRemotelyAsync to set a random TTL, the cache entry is cloned.
            SetOrExtendLocally(key, dataSourceEntry.Clone(), localCacheEntry);
            return dataSourceEntry;
        }

        /// <summary>Checks if a <see cref="CacheEntry{TValue}"/> is stale.</summary>
        private bool IsCacheEntryStale(CacheEntry<TValue> entry)
        {
            return entry.CreationTime + entry.TimeToLive < _dateTime.UtcNow;
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
                using CancellationTokenSource cts = new(_options.LoadTimeout);
                await using var results = _dataSource.LoadAsync(new[] { key }, cts.Token)
                    .GetAsyncEnumerator(cts.Token);
                if (!await results.MoveNextAsync())
                {
                    return (true, null);
                }

                var dataSourceResult = results.Current;
                return (true, NewCacheEntry(dataSourceResult.Value, dataSourceResult.TimeToLive));
            }
            catch
            {
                return (false, null);
            }
        }

        private CacheEntry<TValue> NewCacheEntry(TimeSpan timeToLive)
        {
            return InitCacheEntry(new CacheEntry<TValue>(), timeToLive);
        }

        private CacheEntry<TValue> NewCacheEntry(TValue value, TimeSpan timeToLive)
        {
            return InitCacheEntry(new CacheEntry<TValue>(value), timeToLive);
        }

        private CacheEntry<TValue> InitCacheEntry(CacheEntry<TValue> entry, TimeSpan timeToLive)
        {
            entry.CreationTime = _dateTime.UtcNow;
            entry.TimeToLive = timeToLive;
            return entry;
        }

        private TimeSpan RandomizeTimeSpan(TimeSpan ts)
        {
            double seconds = ts.TotalSeconds;
            seconds -= _random.Next(0, (int)(0.05 * seconds));
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
