using System;
using ModernCaching.LocalCaching;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class InMemoryCacheTest
    {
        [Test]
        public void BasicTests()
        {
            InMemoryCache<int, int> cache = new();
            Assert.IsFalse(cache.TryGet(5, out _));
            CacheEntry<int> entry1 = new(10, DateTime.Now);
            cache.Set(5, entry1);
            Assert.IsTrue(cache.TryGet(5, out var entry2));
            Assert.AreEqual(entry1, entry2);
            cache.Remove(5);
            Assert.IsFalse(cache.TryGet(5, out _));
        }
    }
}
