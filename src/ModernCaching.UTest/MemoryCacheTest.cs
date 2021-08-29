using System;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.LocalCaching;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class MemoryCacheTest
    {
        [Test]
        public void BasicTests()
        {
            MemoryCache<int, int> cache = new();
            Assert.Zero(cache.Count);
            Assert.IsFalse(cache.TryGet(5, out _));

            CacheEntry<int> entry1 = new(10) { ExpirationTime = DateTime.Now, EvictionTime = DateTime.MaxValue };
            cache.Set(5, entry1);
            Assert.AreEqual(1, cache.Count);

            Assert.IsTrue(cache.TryGet(5, out var entry2));
            Assert.AreEqual(entry1, entry2);

            CacheEntry<int> entry3 = new(20) { ExpirationTime = DateTime.Now, EvictionTime = DateTime.MaxValue };
            cache.Set(5, entry3);
            Assert.AreEqual(1, cache.Count);

            Assert.IsTrue(cache.TryGet(5, out var entry4));
            Assert.AreEqual(entry3, entry4);

            CacheEntry<int> entry5 = new(30) { ExpirationTime = DateTime.Now, EvictionTime = DateTime.MaxValue };
            cache.Set(10, entry5);
            Assert.AreEqual(2, cache.Count);

            cache.Delete(5);
            Assert.AreEqual(1, cache.Count);

            Assert.IsFalse(cache.TryGet(5, out _));

            cache.Delete(5);
            Assert.AreEqual(1, cache.Count);
        }

        [Test]
        public void TestCount()
        {
            const int threads = 4;
            MemoryCache<int, int> cache = new();
            Barrier barrier = new(threads);
            Parallel.For(0, threads, _ =>
            {
                for (int i = 0; i < 100; i += 1)
                {
                    barrier.SignalAndWait();
                    cache.Set(i, new CacheEntry<int>());
                }
            });

            Assert.AreEqual(100, cache.Count);
        }
    }
}
