using System;
using ModernCaching.LocalCaching;
using ModernCaching.Utils;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class ReadOnlyCacheTest_TryGet
    {
        [Test]
        public void GettingNullKeyShouldThrow()
        {
            ReadOnlyCache<string, string> cache = new(null, null, null!, Mock.Of<ITimer>());
            Assert.Throws<ArgumentNullException>(() => cache.TryGet(null!, out _));
        }

        [Test]
        public void ShouldReturnLocalEntryIfExists()
        {
            CacheEntry<int>? entry = new(10, DateTime.Now.AddHours(5));

            Mock<ICache<int, int>> localCacheMock = new();
            localCacheMock
                .Setup(c => c.TryGet(5, out entry))
                .Returns(true);

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, null, null!, Mock.Of<ITimer>());
            Assert.IsTrue(cache.TryGet(5, out int val));
            Assert.AreEqual(10, val);
        }

        [Test]
        public void ShouldReturnLocalEntryEvenIfStaleAndReloadAsynchronously()
        {
            CacheEntry<int>? entry = new(10, DateTime.Now.AddHours(-5));

            Mock<ICache<int, int>> localCacheMock = new();
            localCacheMock
                .Setup(c => c.TryGet(5, out entry))
                .Returns(true);

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, null, null!, Mock.Of<ITimer>());
            Assert.IsTrue(cache.TryGet(5, out int val));
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

            ReadOnlyCache<int, int> cache = new(localCacheMock.Object, null, null!, Mock.Of<ITimer>());
            Assert.IsFalse(cache.TryGet(5, out int val));
            Assert.Zero(val);
        }

        [Test]
        public void ShouldReturnNullIfNullWasCached()
        {
            CacheEntry<object?>? entry = new(null, DateTime.Now.AddHours(5));

            Mock<ICache<int, object?>> localCacheMock = new();
            localCacheMock
                .Setup(c => c.TryGet(5, out entry))
                .Returns(true);

            ReadOnlyCache<int, object?> cache = new(localCacheMock.Object, null, null!, Mock.Of<ITimer>());
            Assert.IsTrue(cache.TryGet(5, out object? val));
            Assert.IsNull(val);
        }
    }
}
