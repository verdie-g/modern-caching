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
    public class ReadOnlyCacheTest_TryGetAsync
    {
        private static readonly ReadOnlyCacheOptions Options = new();
        private static readonly ITimer Timer = Mock.Of<ITimer>();
        private static readonly IDateTime MachineDateTime = new MachineDateTime();
        private static readonly IRandom Random = new ThreadSafeRandom();

        [Test]
        public void GettingNullKeyShouldThrow()
        {
            ReadOnlyCache<string, string> cache = new(null, null, null!, Options, Timer, MachineDateTime, Random);
            Assert.ThrowsAsync<ArgumentNullException>(() => cache.TryGetAsync(null!).AsTask());
        }

        [Theory]
        public async Task ShouldReturnLocalEntryIfExists(bool entryHasValue)
        {
            var localCacheEntry = entryHasValue
                ? new CacheEntry<int>(10) { ExpirationTime = DateTime.Now.AddHours(5), EvictionTime = DateTime.MaxValue }
                : new CacheEntry<int> { ExpirationTime = DateTime.Now.AddHours(5), EvictionTime = DateTime.MaxValue };
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(true);

            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Options, Timer, MachineDateTime, Random);
            var res = await cache.TryGetAsync(5);
            Assert.AreEqual(entryHasValue ? (true, 10) : (false, 0), res);
        }

        [Theory]
        public async Task ShouldReturnRemoteEntryIfNoLocalCache(bool entryHasValue)
        {
            var distributedCacheEntry = entryHasValue
                ? new CacheEntry<int>(10) { ExpirationTime = DateTime.Now.AddHours(5), EvictionTime = DateTime.MaxValue }
                : new CacheEntry<int> { ExpirationTime = DateTime.Now.AddHours(5), EvictionTime = DateTime.MaxValue };
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

            ReadOnlyCache<int, int> cache = new(null, distributedCacheMock.Object, null!, Options, Timer, MachineDateTime, Random);
            var res = await cache.TryGetAsync(5);
            Assert.AreEqual(entryHasValue ? (true, 10) : (false, 0), res);
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

            CacheEntry<int> distributedCacheEntry = new(10) { ExpirationTime = DateTime.Now.AddHours(5), EvictionTime = DateTime.MaxValue };
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Options, Timer, MachineDateTime, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
        }

        [Test]
        public async Task ShouldReturnRemoteEntryIfLocalEntryIsStale()
        {
            CacheEntry<int>? localCacheEntry = new(10) { ExpirationTime = DateTime.UtcNow.AddHours(-5), EvictionTime = DateTime.MaxValue };
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(false);
            localCacheMock
                .Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

            CacheEntry<int> distributedCacheEntry = new(10) { ExpirationTime = DateTime.Now.AddHours(5), EvictionTime = DateTime.MaxValue };
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Options, Timer, MachineDateTime, Random);
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

            CacheEntry<string?> distributedCacheEntry = new(null) { ExpirationTime = DateTime.Now.AddHours(5), EvictionTime = DateTime.MaxValue };
            Mock<IDistributedCache<int, string?>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

            ReadOnlyCache<int, string?> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Options, Timer, MachineDateTime, Random);
            Assert.AreEqual((true, null as string), await cache.TryGetAsync(5));
        }

        [Theory]
        public async Task ShouldReturnStaleLocalEntryIfDistributedCacheUnavailable(bool entryHasValue)
        {
            var localCacheEntry = entryHasValue
                ? new CacheEntry<int>(10) { ExpirationTime = DateTime.Now.AddHours(-5), EvictionTime = DateTime.MaxValue }
                : new CacheEntry<int> { ExpirationTime = DateTime.Now.AddHours(-5), EvictionTime = DateTime.MaxValue };
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(true);

            CacheEntry<int>? remoteCacheEntry = null;
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Error, remoteCacheEntry));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Options, Timer, MachineDateTime, Random);
            var res = await cache.TryGetAsync(5);
            Assert.AreEqual(entryHasValue ? (true, 10) : (false, 0), res);
        }

        [Test]
        public async Task ShouldReturnDataFromSourceIfNoLocalOrRemoteCache()
        {
            Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Returns(CreateDataSourceResults(new DataSourceResult<int, int>(5, 10, TimeSpan.FromHours(5))));

            ReadOnlyCache<int, int> cache = new(null, null, dataSourceMock.Object, Options, Timer, MachineDateTime, Random);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
        }

        [Theory]
        public async Task ShouldCorrectlySetExpirationAndGraceTime()
        {
            CacheEntry<int>? localCacheEntry = null;
            Mock<ICache<int, int>> localCacheMock = new();
            localCacheMock.Setup(c => c.TryGet(5, out localCacheEntry)).Returns(false);

            Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Returns(CreateDataSourceResults(new DataSourceResult<int, int>(5, 10, TimeSpan.FromSeconds(100))));

            Mock<IDateTime> dateTimeMock = new();
            dateTimeMock.Setup(dt => dt.UtcNow).Returns(new DateTime(2000, 1, 1));

            Mock<IRandom> randomMock = new(MockBehavior.Strict);
            randomMock.Setup(r => r.Next(0, 15)).Returns(10);

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, null, dataSourceMock.Object, Options, Timer,
                dateTimeMock.Object, randomMock.Object);
            Assert.AreEqual((true, 10), await cache.TryGetAsync(5));
            localCacheMock.Verify(c => c.Set(5, It.Is<CacheEntry<int>>(e =>
                e.Value == 10
                && e.ExpirationTime == new DateTime(2000, 1, 1, 0, 1, 30)
                && e.EvictionTime == new DateTime(2000, 1, 1, 0, 3, 0))));
        }

        [Theory]
        public async Task ShouldReturnDataFromSourceIfNotAvailableInLocalOrRemote(bool localStale, bool remoteStale)
        {
            CacheEntry<int>? localCacheEntry = localStale ? new(99) { ExpirationTime = DateTime.UtcNow.AddHours(-5), EvictionTime = DateTime.MaxValue } : null;
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(localStale);
            localCacheMock
                .Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 10)));

            CacheEntry<int>? remoteCacheEntry = remoteStale ? new(99) { ExpirationTime = DateTime.UtcNow.AddHours(-5), EvictionTime = DateTime.MaxValue } : null;
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

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, dataSourceMock.Object, Options, Timer, MachineDateTime, Random);
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

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, dataSourceMock.Object, Options, Timer, MachineDateTime, Random);
            Assert.AreEqual((false, 0), await cache.TryGetAsync(5));
            localCacheMock.Verify(c => c.Set(5, It.IsAny<CacheEntry<int>>()), Times.Never);
            distributedCacheMock.Verify(c => c.SetAsync(5, It.IsAny<CacheEntry<int>>()), Times.Never);
        }

        [Test]
        public async Task ShouldReturnFalseIfDataWasDeletedFromSource()
        {
            CacheEntry<int>? localCacheEntry = new(99) { ExpirationTime = DateTime.UtcNow.AddHours(-5), EvictionTime = DateTime.MaxValue };
            Mock<ICache<int, int>> localCacheMock = new(MockBehavior.Strict);
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(true);
            localCacheMock.Setup(c => c.Delete(5));

            CacheEntry<int> remoteCacheEntry = new(99) { ExpirationTime = DateTime.UtcNow.AddHours(-5), EvictionTime = DateTime.MaxValue };
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry));

            Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Returns(CreateDataSourceResults());

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, dataSourceMock.Object, Options, Timer, MachineDateTime, Random);
            Assert.AreEqual((false, 0), await cache.TryGetAsync(5));
            localCacheMock.Verify(c => c.Delete(5), Times.Once);
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
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Returns(CreateDataSourceResults());

            ReadOnlyCacheOptions options = new() { CacheDataSourceMisses = true };

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object,
                dataSourceMock.Object, options, Timer, MachineDateTime, Random);
            Assert.AreEqual((false, 0), await cache.TryGetAsync(5));
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

            var remoteCacheEntry = entryHasValue
                ? new CacheEntry<int>(10) { ExpirationTime = DateTime.Now.AddHours(-5), EvictionTime = DateTime.MaxValue }
                : new CacheEntry<int> { ExpirationTime = DateTime.Now.AddHours(-5), EvictionTime = DateTime.MaxValue };
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, remoteCacheEntry));

            Mock<IDataSource<int, int>> dataSourceMock = new(MockBehavior.Strict);
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), CancellationToken.None))
                .Throws<Exception>();

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, dataSourceMock.Object, Options, Timer, MachineDateTime, Random);
            var res = await cache.TryGetAsync(5);
            Assert.AreEqual(entryHasValue ? (true, 10) : (false, 0), res);
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

            CacheEntry<int> remoteCacheEntry = new(10) { ExpirationTime = DateTime.UtcNow.AddHours(-5), EvictionTime = DateTime.MaxValue };
            Mock<IDistributedCache<int, int>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .Returns(Task.Run(() =>
                {
                    mre.WaitOne();
                    return ((AsyncCacheStatus, CacheEntry<int>?))(AsyncCacheStatus.Hit, remoteCacheEntry);
                }));

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Options, Timer, MachineDateTime, Random);
            var get1 = cache.TryGetAsync(5);
            var get2 = cache.TryGetAsync(5);
            Assert.IsFalse(get1.IsCompleted);
            Assert.IsFalse(get2.IsCompleted);

            mre.Set();
            Assert.AreEqual((true, 10), await get1);
            Assert.AreEqual((true, 10), await get2);
            localCacheMock.Verify(c => c.TryGet(5, out localCacheEntry), Times.Exactly(2));
            distributedCacheMock.Verify(c => c.GetAsync(5), Times.Once);

            // Now check that the cached task for key '5' was deleted.
            localCacheMock.Setup(c => c.Set(5, It.Is<CacheEntry<int>>(e => e.Value == 20)));
            CacheEntry<int> distributedCacheEntry = new(20) { ExpirationTime = DateTime.UtcNow.AddHours(5), EvictionTime = DateTime.MaxValue };
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));
            Assert.AreEqual((true, 20), await cache.TryGetAsync(5));
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
            CacheEntry<string?>? localCacheEntry = oldValue == "NOVALUE"
                ? new() { ExpirationTime = DateTime.UtcNow.AddHours(-5), EvictionTime = DateTime.MaxValue }
                : new(oldValue) { ExpirationTime = DateTime.UtcNow.AddHours(-5), EvictionTime = DateTime.MaxValue };
            Mock<ICache<int, string?>> localCacheMock = new();
            localCacheMock
                .Setup(c => c.TryGet(5, out localCacheEntry))
                .Returns(true);

            CacheEntry<string?> distributedCacheEntry = newValue == "NOVALUE"
                ? new() { ExpirationTime = DateTime.UtcNow.AddHours(5), EvictionTime = DateTime.MaxValue }
                : new(newValue) { ExpirationTime = DateTime.UtcNow.AddHours(5), EvictionTime = DateTime.MaxValue };
            Mock<IDistributedCache<int, string?>> distributedCacheMock = new(MockBehavior.Strict);
            distributedCacheMock
                .Setup(c => c.GetAsync(5))
                .ReturnsAsync((AsyncCacheStatus.Hit, distributedCacheEntry));

            ReadOnlyCache<int, string?> cache = new(localCacheMock.Object, distributedCacheMock.Object, null!, Options,
                Timer, MachineDateTime, Random);
            await cache.TryGetAsync(5);

            if (shouldSet) // If a local entry already exist, ICache.Set is not called.
            {
                localCacheMock.Verify(c => c.Set(5, It.IsAny<CacheEntry<string?>>()));
            }
            else
            {
                Assert.AreEqual(DateTime.UtcNow.AddHours(5).Ticks, localCacheEntry!.ExpirationTime.Ticks, delta: TimeSpan.FromHours(1).Ticks);
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
    }
}
