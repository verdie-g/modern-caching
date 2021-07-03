using System;
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
            Assert.IsFalse(cache.TryGet(5, out _));
            CacheEntry<int> entry1 = new(10) { ExpirationTime = DateTime.Now, EvictionTime = DateTime.MaxValue };
            cache.Set(5, entry1);
            Assert.IsTrue(cache.TryGet(5, out var entry2));
            Assert.AreEqual(entry1, entry2);
            cache.Delete(5);
            Assert.IsFalse(cache.TryGet(5, out _));
        }
    }
}
