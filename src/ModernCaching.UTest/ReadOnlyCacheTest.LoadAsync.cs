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

namespace ModernCaching.UTest
{
    public class ReadOnlyCacheTest_LoadAsync
    {
        private static readonly ITimer Timer = Mock.Of<ITimer>();

        [Test]
        public async Task ShouldLoadKeys()
        {
            Mock<ICache<int, int>> localCacheMock = new();
            Mock<IDistributedCache<int, int>> distributedCacheMock = new();

            // 1: distributed cache error
            CacheEntry<int>? remoteCacheEntry1 = null;
            distributedCacheMock.Setup(c => c.GetAsync(1)).ReturnsAsync((AsyncCacheStatus.Error, remoteCacheEntry1));

            // 2: distributed cache miss and data source miss
            CacheEntry<int>? remoteCacheEntry2 = null;
            distributedCacheMock.Setup(c => c.GetAsync(2)).ReturnsAsync((AsyncCacheStatus.Miss, remoteCacheEntry2));

            // 3: distributed cache miss and data source hit
            CacheEntry<int>? remoteCacheEntry3 = null;
            distributedCacheMock.Setup(c => c.GetAsync(3)).ReturnsAsync((AsyncCacheStatus.Miss, remoteCacheEntry3));

            // 4: distributed cache hit
            CacheEntry<int>? remoteCacheEntry4 = new(44, DateTime.UtcNow.AddHours(5));
            distributedCacheMock.Setup(c => c.GetAsync(4)).ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry4));

            // 5: distributed cache hit stale and data source hit
            CacheEntry<int>? remoteCacheEntry5 = new(55, DateTime.UtcNow.AddHours(-5));
            distributedCacheMock.Setup(c => c.GetAsync(5)).ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry5));

            Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
            dataSourceMock
                .Setup(s => s.LoadAsync(It.Is<IEnumerable<int>>(e => e.Count() == 3), CancellationToken.None))
                .Returns(CreateDataSourceResults(new[]
                {
                    new DataSourceResult<int, int>(3, 333, TimeSpan.FromHours(5)),
                    new DataSourceResult<int, int>(5, 555, TimeSpan.FromHours(5)),
                }));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, dataSourceMock.Object, Timer);
            await cache.LoadAsync(new[] { 1, 2, 3, 4, 5 });

            localCacheMock.Verify(c => c.Set(1, It.IsAny<CacheEntry<int>>()), Times.Never);
            localCacheMock.Verify(c => c.Set(2, It.IsAny<CacheEntry<int>>()), Times.Never);
            localCacheMock.Verify(c => c.Set(3, It.Is<CacheEntry<int>>(e => e.Value == 333)));
            localCacheMock.Verify(c => c.Set(4, It.Is<CacheEntry<int>>(e => e.Value == 44)));
            localCacheMock.Verify(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 555)));

            Assert.That(
                () => distributedCacheMock.Invocations.Count(i => i.Method.Name == nameof(IAsyncCache.SetAsync)) == 2,
                Is.True.After(5000, 100));
        }

        [Test]
        public void ShouldThrowIfDataSourceThrows()
        {
            Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Throws<Exception>();

            ReadOnlyCache<int, int> cache = new(null, null, dataSourceMock.Object, Timer);
            Assert.ThrowsAsync<Exception>(() => cache.LoadAsync(Array.Empty<int>()));
        }

        private async IAsyncEnumerable<DataSourceResult<int, int>> CreateDataSourceResults(params DataSourceResult<int, int>[] results)
        {
            foreach (var result in results)
            {
                yield return result;
            }
        }
    }
}
