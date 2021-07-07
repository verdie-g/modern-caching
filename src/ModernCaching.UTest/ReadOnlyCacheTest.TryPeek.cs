using System;
using ModernCaching.LocalCaching;
using ModernCaching.Utils;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class ReadOnlyCacheTest_TryPeek
    {
        private static readonly ITimer Timer = Mock.Of<ITimer>();
        private static readonly IDateTime MachineDateTime = new MachineDateTime();
        private static readonly IRandom Random = new ThreadSafeRandom();

        [Test]
        public void GettingNullKeyShouldThrow()
        {
            ReadOnlyCache<string, string> cache = new(null, null, null!, Timer, MachineDateTime, Random);
            Assert.Throws<ArgumentNullException>(() => cache.TryPeek(null!, out _));
        }

        [Test]
        public void ShouldReturnLocalEntryIfExists()
        {
            CacheEntry<int>? entry = new(10) { ExpirationTime = DateTime.Now.AddHours(5), EvictionTime = DateTime.MaxValue };

            Mock<ICache<int, int>> localCacheMock = new();
            localCacheMock
                .Setup(c => c.TryGet(5, out entry))
                .Returns(true);

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, null, null!, Timer, MachineDateTime, Random);
            Assert.IsTrue(cache.TryPeek(5, out int val));
            Assert.AreEqual(10, val);
        }

        [Test]
        public void ShouldReturnLocalEntryEvenIfStaleAndReloadAsynchronously()
        {
            CacheEntry<int>? entry = new(10) { ExpirationTime = DateTime.Now.AddHours(-5), EvictionTime = DateTime.MaxValue };

            Mock<ICache<int, int>> localCacheMock = new();
            localCacheMock
                .Setup(c => c.TryGet(5, out entry))
                .Returns(true);

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, null, null!, Timer, MachineDateTime, Random);
            Assert.IsTrue(cache.TryPeek(5, out int val));
            Assert.AreEqual(10, val);
        }

        [Test]
        public void ShouldReturnDefaultIfKeyWasNotPresentInLocalCache()
        {
            CacheEntry<int>? entry = null;

            Mock<ICache<int, int>> localCacheMock = new();
            localCacheMock
                .Setup(c => c.TryGet(5, out entry))
                .Returns(false);

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, null, null!, Timer, MachineDateTime, Random);
            Assert.IsFalse(cache.TryPeek(5, out int val));
            Assert.Zero(val);
        }

        [Test]
        public void ShouldReturnNullIfNullWasCached()
        {
            CacheEntry<object?>? entry = new(null) { ExpirationTime = DateTime.Now.AddHours(5), EvictionTime = DateTime.MaxValue };

            Mock<ICache<int, object?>> localCacheMock = new();
            localCacheMock
                .Setup(c => c.TryGet(5, out entry))
                .Returns(true);

            ReadOnlyCache<int, object?> cache = new(localCacheMock.Object, null, null!, Timer, MachineDateTime, Random);
            Assert.IsTrue(cache.TryPeek(5, out object? val));
            Assert.IsNull(val);
        }
    }
}
