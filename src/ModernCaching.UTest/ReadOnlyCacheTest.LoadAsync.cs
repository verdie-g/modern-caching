using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;
using ModernCaching.Utils;
using Moq;
using NUnit.Framework;
using ITimer = ModernCaching.Utils.ITimer;

namespace ModernCaching.UTest;

public class ReadOnlyCacheTest_LoadAsync
{
    private const string C = "cache_test";
    private static readonly ReadOnlyCacheOptions Options = new();
#pragma warning disable NUnit1032
    private static readonly ITimer Timer = Mock.Of<ITimer>();
#pragma warning restore NUnit1032
    private static readonly IDateTime MachineDateTime = new CachedDateTime(Timer);

    [Theory]
    public async Task ShouldLoadKeys(bool cacheDataSourceMisses)
    {
        Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
        Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);

        // 1: distributed cache error
        CacheEntry<int>? remoteCacheEntry1 = null;
        distributedCacheMock.Setup(c => c.GetAsync(1)).ReturnsAsync((AsyncCacheStatus.Error, remoteCacheEntry1));

        // 2: distributed cache miss and data source miss
        CacheEntry<int>? remoteCacheEntry2 = null;
        distributedCacheMock.Setup(c => c.GetAsync(2)).ReturnsAsync((AsyncCacheStatus.Miss, remoteCacheEntry2));
        if (cacheDataSourceMisses)
        {
            localCacheMock.Setup(c => c.Set(2, It.Is<CacheEntry<int>>(e => !e.HasValue)));
        }
        else
        {
            localCacheMock.Setup(c => c.TryDelete(2)).Returns(false);
        }

        // 3: distributed cache miss and data source hit
        CacheEntry<int>? remoteCacheEntry3 = null;
        distributedCacheMock.Setup(c => c.GetAsync(3)).ReturnsAsync((AsyncCacheStatus.Miss, remoteCacheEntry3));
        localCacheMock.Setup(c => c.Set(3, It.Is<CacheEntry<int>>(e => e.Value == 333)));

        // 4: distributed cache hit
        CacheEntry<int> remoteCacheEntry4 = new(44) { CreationTime = DateTime.UtcNow, TimeToLive = TimeSpan.FromHours(1) };
        distributedCacheMock.Setup(c => c.GetAsync(4)).ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry4));
        localCacheMock.Setup(c => c.Set(4, remoteCacheEntry4));

        // 5: distributed cache hit stale and data source hit
        CacheEntry<int> remoteCacheEntry5 = new(55) { CreationTime = DateTime.UtcNow.AddHours(-5), TimeToLive = TimeSpan.FromHours(1) };
        distributedCacheMock.Setup(c => c.GetAsync(5)).ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry5));
        localCacheMock.Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 555)));

        // 6: distributed cache hit stale and data source miss
        CacheEntry<int> remoteCacheEntry6 = new(66) { CreationTime = DateTime.UtcNow.AddHours(-5), TimeToLive = TimeSpan.FromHours(1) };
        distributedCacheMock.Setup(c => c.GetAsync(6)).ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry6));
        if (cacheDataSourceMisses)
        {
            localCacheMock.Setup(c => c.Set(6, It.Is<CacheEntry<int>>(e => !e.HasValue)));
        }
        else
        {
            localCacheMock.Setup(c => c.TryDelete(6)).Returns(true);
        }

        Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.Is<IEnumerable<int>>(e => e.Count() == 4), It.IsAny<CancellationToken>()))
            .Returns(CreateDataSourceResults(new[]
            {
                new DataSourceResult<int, int>(3, 333, TimeSpan.FromHours(5)),
                new DataSourceResult<int, int>(5, 555, TimeSpan.FromHours(5)),
            }));

        ReadOnlyCacheOptions options = new() { CacheDataSourceMisses = cacheDataSourceMisses };

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, distributedCacheMock.Object,
            dataSourceMock.Object, options, Timer, MachineDateTime, Random.Shared);
        await cache.LoadAsync(new[] { 1, 2, 3, 4, 5, 6 });

        int expectedSetAsyncInvocations = cacheDataSourceMisses ? 4 : 2;
        Assert.That(
            () => distributedCacheMock.Invocations.Count(i => i.Method.Name == nameof(IAsyncCache.SetAsync)) == expectedSetAsyncInvocations,
            Is.True.After(5000, 100));
        int expectedDeleteAsyncInvocations = cacheDataSourceMisses ? 0 : 1;
        Assert.That(
            () => distributedCacheMock.Invocations.Count(i => i.Method.Name == nameof(IAsyncCache.DeleteAsync)) == expectedDeleteAsyncInvocations,
            Is.True.After(5000, 100));

        localCacheMock.VerifyAll();
        distributedCacheMock.VerifyAll();
        dataSourceMock.VerifyAll();
    }

    [Test]
    public async Task ShouldNotCallLoaderIfNoKeys()
    {
        Mock<IDataSource<int, int>> dataSourceMock = new();

        ReadOnlyCache<int, int> cache = new(C, null, null, dataSourceMock.Object, Options, Timer, MachineDateTime,
            Random.Shared);
        await cache.LoadAsync(Array.Empty<int>());

        dataSourceMock.Verify(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task ShouldChunkKeys()
    {
        Mock<IDataSource<int, int>> dataSourceMock = new();
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateDataSourceResults());

        ReadOnlyCache<int, int> cache = new(C, null, null, dataSourceMock.Object, Options, Timer, MachineDateTime,
            Random.Shared);
        await cache.LoadAsync(Enumerable.Range(1, 5432));

        dataSourceMock.Verify(s => s.LoadAsync(
                It.Is<IEnumerable<int>>(e => e.Count() == 1000),
                It.IsAny<CancellationToken>()),
            Times.Exactly(5));
        dataSourceMock.Verify(s => s.LoadAsync(
                It.Is<IEnumerable<int>>(e => e.Count() == 432),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public void ShouldThrowIfDataSourceThrows()
    {
        Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Throws<Exception>();

        ReadOnlyCache<int, int> cache = new(C, null, null, dataSourceMock.Object, Options, Timer,
            MachineDateTime, Random.Shared);
        Assert.ThrowsAsync<Exception>(() => cache.LoadAsync(new[] { 0 }));
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
}