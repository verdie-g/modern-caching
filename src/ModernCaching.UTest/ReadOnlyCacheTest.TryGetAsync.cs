using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;
using ModernCaching.Utils;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class ReadOnlyCacheTest_TryGetAsync
{
    private const string C = "cache_test";
    private static readonly ReadOnlyCacheOptions Options = new();
#pragma warning disable NUnit1032
    private static readonly ITimer Timer = Mock.Of<ITimer>();
#pragma warning restore NUnit1032
    private static readonly IDateTime MachineDateTime = new CachedDateTime(Timer);

    [Test]
    public void GettingNullKeyShouldThrow()
    {
        ReadOnlyCache<string, string> cache = new(C, null, null, null!, Options, Timer, MachineDateTime, Random.Shared);
        Assert.ThrowsAsync<ArgumentNullException>(() => cache.TryGetAsync(null!).AsTask());
    }

    [Theory]
    public async Task ShouldReturnLocalEntryIfExists(bool entryHasValue)
    {
        var localCacheEntry = entryHasValue ? FreshEntry(10) : FreshEntry<int>();
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(true);

        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object, null!, Options,
            Timer, MachineDateTime, Random.Shared);
        var res = await cache.TryGetAsync(5);
        Assert.That(res, Is.EqualTo(entryHasValue ? (true, 10) : (false, 0)));
    }

    [Theory]
    public async Task ShouldReturnRemoteEntryIfNoLocalCache(bool entryHasValue)
    {
        var distributedCacheEntry = entryHasValue ? FreshEntry(10) : FreshEntry<int>();
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

        ReadOnlyCache<int, int> cache = new(C, null, distributedCacheMock.Object, null!, Options, Timer,
            MachineDateTime, Random.Shared);
        var res = await cache.TryGetAsync(5);
        Assert.That(res, Is.EqualTo(entryHasValue ? (true, 10) : (false, 0)));
    }

    [Test]
    public async Task ShouldReturnRemoteEntryIfLocalKeyDoesntExist()
    {
        CacheEntry<int>? localCacheEntry = null;
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(false);
        localCacheMock
            .Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

        CacheEntry<int> distributedCacheEntry = FreshEntry(10);
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object, null!, Options,
            Timer, MachineDateTime, Random.Shared);
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((true, 10)));
    }

    [Test]
    public async Task ShouldReturnRemoteEntryIfLocalEntryIsStale()
    {
        CacheEntry<int>? localCacheEntry = StaleEntry(10);
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(false);
        localCacheMock
            .Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

        CacheEntry<int> distributedCacheEntry = FreshEntry(10);
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object, null!, Options,
            Timer, MachineDateTime, Random.Shared);
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((true, 10)));
    }

    [Test]
    public async Task ShouldReturnNullIfNullWasCachedInRemote()
    {
        CacheEntry<string?>? localCacheEntry = null;
        Mock<ICache<int, string?>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(false);
        localCacheMock
            .Setup(c => c.Set(5, It.Is<CacheEntry<string?>>(e => e.Value == null)));

        CacheEntry<string?> distributedCacheEntry = FreshEntry<string?>(null);
        Mock<IDistributedCache<int, string?>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

        ReadOnlyCache<int, string?> cache = new(C, localCacheMock.Object, distributedCacheMock.Object, null!,
            Options, Timer, MachineDateTime, Random.Shared);
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((true, null as string)));
    }

    [Theory]
    public async Task ShouldReturnStaleLocalEntryIfDistributedCacheUnavailable(bool entryHasValue)
    {
        var localCacheEntry = entryHasValue ? StaleEntry(10) : StaleEntry<int>();
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(true);

        CacheEntry<int>? remoteCacheEntry = null;
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Error, remoteCacheEntry));

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object, null!, Options,
            Timer, MachineDateTime, Random.Shared);
        var res = await cache.TryGetAsync(5);
        Assert.That(res, Is.EqualTo(entryHasValue ? (true, 10) : (false, 0)));
    }

    [Test]
    public async Task ShouldReturnDataFromSourceIfNoLocalOrRemoteCache()
    {
        Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateDataSourceResults(new DataSourceResult<int, int>(5, 10, TimeSpan.FromHours(5))));

        ReadOnlyCache<int, int> cache = new(C, null, null, dataSourceMock.Object, Options, Timer, MachineDateTime,
            Random.Shared);
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((true, 10)));
    }

    [Theory]
    public async Task ShouldCorrectlySetCreationTimeAndTimeToLive()
    {
        CacheEntry<int>? localCacheEntry = null;
        Mock<ICache<int, int>> localCacheMock = new();
        localCacheMock.Setup(c => c.TryGet(5, out localCacheEntry)).Returns(false);

        Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateDataSourceResults(new DataSourceResult<int, int>(5, 10, TimeSpan.FromSeconds(100))));

        Mock<IDateTime> dateTimeMock = new();
        dateTimeMock.Setup(dt => dt.UtcNow).Returns(new DateTime(2000, 1, 1));

        Mock<Random> randomMock = new(MockBehavior.Strict);
        randomMock.Setup(r => r.Next(0, 5)).Returns(2);

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, null, dataSourceMock.Object, Options, Timer,
            dateTimeMock.Object, randomMock.Object);
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((true, 10)));
        localCacheMock.Verify(c => c.Set(5, It.Is<CacheEntry<int>>(e =>
            e.Value == 10
            && e.CreationTime == new DateTime(2000, 1, 1)
            && e.TimeToLive == TimeSpan.FromSeconds(98))));
    }

    [Theory]
    public async Task ShouldReturnDataFromSourceIfNotAvailableInLocalOrRemote(bool localStale, bool remoteStale)
    {
        CacheEntry<int>? localCacheEntry = localStale ? StaleEntry(99) : null;
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(localStale);
        localCacheMock
            .Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

        CacheEntry<int>? remoteCacheEntry = remoteStale ? StaleEntry(99) : null;
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((remoteStale ? AsyncCacheStatus.Hit : AsyncCacheStatus.Miss, remoteCacheEntry));
        distributedCacheMock
            .Setup(c => c.SetAsync(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

        Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateDataSourceResults(new DataSourceResult<int, int>(5, 10, TimeSpan.FromHours(5))));

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object,
            dataSourceMock.Object, Options, Timer, MachineDateTime, Random.Shared);
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((true, 10)));
        localCacheMock.Verify(c => c.Set(5, It.IsAny<CacheEntry<int>>()));
        Assert.That(() => distributedCacheMock.Invocations.Any(i => i.Method.Name == nameof(IAsyncCache.SetAsync)),
            Is.True.After(5000, 100));
    }

    [Test]
    public async Task ShouldReturnFalseIfDataIsNotPresentInAnyLayer()
    {
        CacheEntry<int>? localCacheEntry = null;
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(false);

        CacheEntry<int>? remoteCacheEntry = null;
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Miss, remoteCacheEntry));

        Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateDataSourceResults());

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object,
            dataSourceMock.Object, Options, Timer, MachineDateTime, Random.Shared);
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((false, 0)));
        localCacheMock.Verify(c => c.Set(5, It.IsAny<CacheEntry<int>>()), Times.Never);
        distributedCacheMock.Verify(c => c.SetAsync(5, It.IsAny<CacheEntry<int>>()), Times.Never);
    }

    [Test]
    public async Task ShouldReturnFalseIfDataWasDeletedFromSource()
    {
        CacheEntry<int>? localCacheEntry = StaleEntry(99);
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(true);
        localCacheMock.Setup(c => c.TryDelete(5)).Returns(true);

        CacheEntry<int> remoteCacheEntry = StaleEntry(99);
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry));

        Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateDataSourceResults());

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object,
            dataSourceMock.Object, Options, Timer, MachineDateTime, Random.Shared);
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((false, 0)));
        localCacheMock.Verify(c => c.TryDelete(5), Times.Once);
        Assert.That(() => distributedCacheMock.Invocations.Any(i => i.Method.Name == nameof(IAsyncCache.DeleteAsync)),
            Is.True.After(5000, 100));
    }

    [Test]
    public async Task ShouldCacheNoValueIfCacheDataSourceMissesOptionIsUsed()
    {
        CacheEntry<int>? localCacheEntry = null;
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(false);
        localCacheMock.Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.HasValue == false)));

        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Miss, null));
        distributedCacheMock.Setup(c => c.SetAsync(5, It.Is<CacheEntry<int>>(e => e.HasValue == false)));

        Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateDataSourceResults());

        ReadOnlyCacheOptions options = new() { CacheDataSourceMisses = true };

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object,
            dataSourceMock.Object, options, Timer, MachineDateTime, Random.Shared);
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((false, 0)));
        localCacheMock.Verify(c => c.Set(5, It.IsAny<CacheEntry<int>>()), Times.Once);
        Assert.That(() => distributedCacheMock.Invocations.Any(i => i.Method.Name == nameof(IAsyncCache.SetAsync)),
            Is.True.After(5000, 100));
    }

    [Theory]
    public async Task ShouldReturnStaleRemoteEntryIfDataSourceUnavailable(bool entryHasValue)
    {
        CacheEntry<int>? localCacheEntry = null;
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(false);

        var remoteCacheEntry = entryHasValue ? StaleEntry(10) : StaleEntry<int>();
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry));

        Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Throws<Exception>();

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object,
            dataSourceMock.Object, Options, Timer, MachineDateTime, Random.Shared);
        var res = await cache.TryGetAsync(5);
        Assert.That(res, Is.EqualTo(entryHasValue ? (true, 10) : (false, 0)));
    }

    [Test]
    public async Task ShouldMultiplexConcurrentGets()
    {
        ManualResetEvent mre = new(false);

        CacheEntry<int>? localCacheEntry = null;
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(false);
        localCacheMock.Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

        CacheEntry<int> remoteCacheEntry = StaleEntry(10);
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .Returns(Task.Run(() =>
            {
                mre.WaitOne();
                return ((AsyncCacheStatus, CacheEntry<int>?))(AsyncCacheStatus.Hit, remoteCacheEntry);
            }));

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object, null!, Options,
            Timer, MachineDateTime, Random.Shared);
        var get1 = cache.TryGetAsync(5);
        var get2 = cache.TryGetAsync(5);
        Assert.That(get1.IsCompleted, Is.False);
        Assert.That(get2.IsCompleted, Is.False);

        mre.Set();
        Assert.That(await get1, Is.EqualTo((true, 10)));
        Assert.That(await get2, Is.EqualTo((true, 10)));
        localCacheMock.Verify(c => c.TryGet(5, out localCacheEntry), Times.Exactly(2));
        distributedCacheMock.Verify(c => c.GetAsync(5), Times.Once);

        // Now check that the cached task for key '5' was deleted.
        localCacheMock.Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 20)));
        CacheEntry<int> distributedCacheEntry = FreshEntry(20);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));
        Assert.That(await cache.TryGetAsync(5), Is.EqualTo((true, 20)));
    }

    [TestCase("A", "A", false)]
    [TestCase("A", "B", true)]
    [TestCase("A", null, true)]
    [TestCase("A", "NOVALUE", true)]
    [TestCase(null, "A", true)]
    [TestCase(null, "NOVALUE", true)]
    [TestCase("NOVALUE", "NOVALUE", false)]
    [TestCase("NOVALUE", "A", true)]
    [TestCase("NOVALUE", null, true)]
    [Description("Replace expired local cache entry by the one distributed cache one and check if the local entry was replaced or extended")]
    public async Task SetOrExtendTest(string? oldValue, string? newValue, bool shouldSet)
    {
        CacheEntry<string?>? localCacheEntry = oldValue == "NOVALUE" ? StaleEntry<string?>() : StaleEntry(oldValue);
        Mock<ICache<int, string?>> localCacheMock = new();
        localCacheMock
            .Setup(c => c.TryGet(5, out localCacheEntry))
            .Returns(true);

        CacheEntry<string?> distributedCacheEntry = newValue == "NOVALUE"
            ? FreshEntry<string?>()
            : FreshEntry(newValue);
        Mock<IDistributedCache<int, string?>> distributedCacheMock = new(MockBehavior.Strict);
        distributedCacheMock
            .Setup(c => c.GetAsync(5))
            .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

        ReadOnlyCache<int, string?> cache = new(C, localCacheMock.Object, distributedCacheMock.Object, null!, Options,
            Timer, MachineDateTime, Random.Shared);
        await cache.TryGetAsync(5);

        if (shouldSet) // If a local entry already exist, ICache.Set is not called.
        {
            localCacheMock.Verify(c => c.Set(5, It.IsAny<CacheEntry<string?>>()));
        }
        else
        {
            Assert.That(localCacheEntry.CreationTime.Ticks, Is.EqualTo(DateTime.UtcNow.Ticks).Within(TimeSpan.FromMinutes(5).Ticks));
            Assert.That(localCacheEntry.TimeToLive.Ticks, Is.EqualTo(TimeSpan.FromHours(1).Ticks).Within(TimeSpan.FromMinutes(5).Ticks));
        }
    }

    [Test, Description("Test the TTL are randomized locally but not in the distributed cache")]
    public async Task TimeToLiveShouldBeRandomizedLocally()
    {
        DictionaryDistributedCache<int, int> distributedCache = new();
        Mock<IDataSource<int, int>> dataSourceMock = new();
        Mock<IDateTime> dateTimeMock = new();

        var ttl = TimeSpan.FromSeconds(10000);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<int> keys, CancellationToken _) => CreateDataSourceResults(new DataSourceResult<int, int>(keys.First(), keys.First(), ttl)));

        DictionaryCache<int, int> localCache1 = new();
        ReadOnlyCache<int, int> cache1 = new("yo", localCache1, distributedCache, dataSourceMock.Object,
            new ReadOnlyCacheOptions(), Mock.Of<ITimer>(), dateTimeMock.Object, Random.Shared);
        DictionaryCache<int, int> localCache2 = new();
        ReadOnlyCache<int, int> cache2 = new("yo", localCache2, distributedCache, dataSourceMock.Object,
            new ReadOnlyCacheOptions(), Mock.Of<ITimer>(), dateTimeMock.Object, Random.Shared);
        DictionaryCache<int, int> localCache3 = new();
        ReadOnlyCache<int, int> cache3 = new("yo", localCache3, distributedCache, dataSourceMock.Object,
            new ReadOnlyCacheOptions(), Mock.Of<ITimer>(), dateTimeMock.Object, Random.Shared);

        for (int i = 0; i < 10_000; i += 1)
        {
            await cache1.TryGetAsync(i);
            await cache2.TryGetAsync(i);
            await cache3.TryGetAsync(i);
        }

        // Wait for all distributed cache sets to be performed.
        await Task.Delay(TimeSpan.FromSeconds(5));

        for (int i = 0; i < 10_000; i += 1)
        {
            Assert.That(localCache1.Dictionary[i].TimeToLive.TotalSeconds, Is.EqualTo(ttl.TotalSeconds - 500).Within(500));
            Assert.That(localCache2.Dictionary[i].TimeToLive.TotalSeconds, Is.EqualTo(ttl.TotalSeconds - 500).Within(500));
            Assert.That(localCache3.Dictionary[i].TimeToLive.TotalSeconds, Is.EqualTo(ttl.TotalSeconds - 500).Within(500));
            Assert.That(distributedCache.Dictionary[i].TimeToLive, Is.EqualTo(ttl));
        }
    }

#pragma warning disable 1998
    private async IAsyncEnumerable<DataSourceResult<int, int>> CreateDataSourceResults(params DataSourceResult<int, int>[] results)
#pragma warning restore 1998
    {
        foreach (var result in results)
        {
            yield return result;
        }
    }

    private CacheEntry<TValue> FreshEntry<TValue>()
    {
        return new CacheEntry<TValue>
        {
            CreationTime = DateTime.UtcNow,
            TimeToLive = TimeSpan.FromHours(1),
        };
    }

    private CacheEntry<TValue> FreshEntry<TValue>(TValue value)
    {
        return new CacheEntry<TValue>(value)
        {
            CreationTime = DateTime.UtcNow,
            TimeToLive = TimeSpan.FromHours(1),
        };
    }

    private CacheEntry<TValue> StaleEntry<TValue>()
    {
        return new CacheEntry<TValue>()
        {
            CreationTime = DateTime.UtcNow.AddHours(-5),
            TimeToLive = TimeSpan.FromHours(1),
        };
    }

    private CacheEntry<TValue> StaleEntry<TValue>(TValue value)
    {
        return new CacheEntry<TValue>(value)
        {
            CreationTime = DateTime.UtcNow.AddHours(-5),
            TimeToLive = TimeSpan.FromHours(1),
        };
    }

    private class DictionaryCache<TKey, TValue> : ICache<TKey, TValue> where TKey : notnull
    {
        public ConcurrentDictionary<TKey, CacheEntry<TValue>> Dictionary { get; } = new();
        public int Count => Dictionary.Count;

        public bool TryGet(TKey key, [MaybeNullWhen(false)] out CacheEntry<TValue> entry)
        {
            return Dictionary.TryGetValue(key, out entry);
        }

        public void Set(TKey key, CacheEntry<TValue> entry)
        {
            Dictionary[key] = entry;
        }

        public bool TryDelete(TKey key)
        {
            return Dictionary.TryRemove(key, out _);
        }
    }

    private class DictionaryDistributedCache<TKey, TValue> : IDistributedCache<TKey, TValue> where TKey : notnull
    {
        public ConcurrentDictionary<TKey, CacheEntry<TValue>> Dictionary { get; } = new();

        public Task<(AsyncCacheStatus status, CacheEntry<TValue>? entry)> GetAsync(TKey key)
        {
            return Task.FromResult(Dictionary.TryGetValue(key, out var entry)
                ? (AsyncCacheStatus.Hit, entry.Clone())
                : (AsyncCacheStatus.Miss, default));
        }

        public Task SetAsync(TKey key, CacheEntry<TValue> entry)
        {
            Dictionary[key] = entry;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(TKey key)
        {
            Dictionary.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }
}
