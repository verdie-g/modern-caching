using System;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.LocalCaching;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class MemoryCacheTest
{
    [Test]
    public void BasicTests()
    {
        MemoryCache<int, int> cache = new();
        Assert.Zero(cache.Count);
        Assert.IsFalse(cache.TryGet(5, out _));

        CacheEntry<int> entry1 = new(10);
        cache.Set(5, entry1);
        Assert.AreEqual(1, cache.Count, "Setting a new key should increase the count");

        Assert.IsTrue(cache.TryGet(5, out var entry2), "New key should be available in the cache");
        Assert.AreEqual(entry1, entry2);

        CacheEntry<int> entry3 = new(20);
        cache.Set(5, entry3);
        Assert.AreEqual(1, cache.Count, "Setting an existing key should not increase the count");

        Assert.IsTrue(cache.TryGet(5, out var entry4));
        Assert.AreEqual(entry3, entry4);

        CacheEntry<int> entry5 = new(30);
        cache.Set(10, entry5);
        Assert.AreEqual(2, cache.Count, "Setting a new key should increase the count");

        Assert.IsTrue(cache.TryDelete(5), "Deleting an existing key should return true");
        Assert.AreEqual(1, cache.Count, "Removing an existing key should decrease the count");

        Assert.IsFalse(cache.TryGet(5, out _), "Getting an unknown key should return false");

        Assert.IsFalse(cache.TryDelete(5), "Deleting an unknown key should return false");
        Assert.AreEqual(1, cache.Count, "Removing an unknown key should not decrease the count");
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