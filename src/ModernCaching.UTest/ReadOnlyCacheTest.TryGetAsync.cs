﻿using System;
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
    public class ReadOnlyCacheTest_TryGetAsync
    {
        private static readonly ITimer Timer = Mock.Of<ITimer>();
        private static readonly IRandom Random = new ThreadSafeRandom();

        [Test]
        public void GettingNullKeyShouldThrow()
        {
            ReadOnlyCache<string, string> cache = new(null, null, null!, Timer, Random);
            Assert.ThrowsAsync<ArgumentNullException>(() => cache.TryGetAsync(null!).AsTask());
        }

        [Test]
        public async Task ShouldReturnLocalEntryIfExists()
        {
            CacheEntry<int>? localCacheEntry = new(10, DateTime.UtcNow.AddHours(5));
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(true);

            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Timer, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
        }

        [Test]
        public async Task ShouldReturnRemoteEntryIfNoLocalCache()
        {
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, new(10, DateTime.Now.AddHours(5))));

            ReadOnlyCache<int, int> cache = new(null, distributedCacheMock.Object, null!, Timer, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
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

            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, new(10, DateTime.Now.AddHours(5))));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Timer, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
        }

        [Test]
        public async Task ShouldReturnRemoteEntryIfLocalEntryIsStale()
        {
            CacheEntry<int>? localCacheEntry = new(10, DateTime.UtcNow.AddHours(-5));
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(false);
            localCacheMock
                .Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, new(10, DateTime.Now.AddHours(5))));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Timer, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
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

            Mock<IDistributedCache<int, string?>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, new(null, DateTime.Now.AddHours(5))));

            ReadOnlyCache<int, string?> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Timer, Random);
            Assert.AreEqual((true, null as string), await cache.TryGetAsync(5));
        }

        [Test]
        public async Task ShouldReturnStaleLocalEntryIfDistributedCacheUnavailable()
        {
            CacheEntry<int>? localCacheEntry = new(10, DateTime.UtcNow.AddHours(-5));
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(true);

            CacheEntry<int>? remoteCacheEntry = null;
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Error, remoteCacheEntry));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Timer, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
        }

        [Test]
        public async Task ShouldReturnDataFromSourceIfNoLocalOrRemoteCache()
        {
            Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Returns(CreateDataSourceResults(new DataSourceResult<int, int>(5, 10, TimeSpan.FromHours(5))));

            ReadOnlyCache<int, int> cache = new(null, null, dataSourceMock.Object, Timer, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
        }

        [Theory]
        public async Task ShouldReturnDataFromSourceIfNotAvailableInLocalOrRemote(bool localStale, bool remoteStale)
        {
            CacheEntry<int>? localCacheEntry = localStale ? new(99, DateTime.UtcNow.AddHours(-5)) : null;
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(localStale);
            localCacheMock
                .Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

            CacheEntry<int>? remoteCacheEntry = remoteStale ? new(99, DateTime.UtcNow.AddHours(-5)) : null;
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((remoteStale ? AsyncCacheStatus.Hit : AsyncCacheStatus.Miss, remoteCacheEntry));
            distributedCacheMock
                .Setup(c => c.SetAsync(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

            Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Returns(CreateDataSourceResults(new DataSourceResult<int, int>(5, 10, TimeSpan.FromHours(5))));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, dataSourceMock.Object, Timer, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
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
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Returns(CreateDataSourceResults());

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, dataSourceMock.Object, Timer, Random);
            Assert.AreEqual((false, 0), await cache.TryGetAsync(5));
            localCacheMock.Verify(c => c.Set(5, It.IsAny<CacheEntry<int>>()), Times.Never);
            distributedCacheMock.Verify(c => c.SetAsync(5, It.IsAny<CacheEntry<int>>()), Times.Never);
        }

        [Test]
        public async Task ShouldReturnStaleRemoteEntryIfDataSourceUnavailable()
        {
            CacheEntry<int>? localCacheEntry = null;
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(false);

            CacheEntry<int>? remoteCacheEntry = new(10, DateTime.UtcNow.AddHours(-5));
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry));

            Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Throws<Exception>();

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, dataSourceMock.Object, Timer, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
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

            CacheEntry<int> remoteCacheEntry = new(10, DateTime.UtcNow.AddHours(-5));
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .Returns(Task.Run(() =>
                {
                    mre.WaitOne();
                    return ((AsyncCacheStatus, CacheEntry<int>?))(AsyncCacheStatus.Hit, remoteCacheEntry);
                }));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Timer, Random);
            var get1 = cache.TryGetAsync(5);
            var get2 = cache.TryGetAsync(5);
            Assert.IsFalse(get1.IsCompleted);
            Assert.IsFalse(get2.IsCompleted);

            mre.Set();
            Assert.AreEqual((true, 10), await get1);
            Assert.AreEqual((true, 10), await get2);
            localCacheMock.Verify(c => c.TryGet(5, out localCacheEntry), Times.Exactly(2));
            distributedCacheMock.Verify(c => c.GetAsync(5), Times.Once);

            // Now check that the cached task for key '5' was removed.
            localCacheMock.Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 20)));
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, new CacheEntry<int>(20, DateTime.UtcNow.AddHours(5))));
            Assert.AreEqual((true, 20), await cache.TryGetAsync(5));
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
