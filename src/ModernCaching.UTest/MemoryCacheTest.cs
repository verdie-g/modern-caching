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
        Assert.That(cache.Count, Is.Zero);
        Assert.That(cache.TryGet(5, out _), Is.False);

        CacheEntry<int> entry1 = new(10);
        cache.Set(5, entry1);
        Assert.That(cache.Count, Is.EqualTo(1), "Setting a new key should increase the count");

        Assert.That(cache.TryGet(5, out var entry2), Is.True, "New key should be available in the cache");
        Assert.That(entry2, Is.EqualTo(entry1));

        CacheEntry<int> entry3 = new(20);
        cache.Set(5, entry3);
        Assert.That(cache.Count, Is.EqualTo(1), "Setting an existing key should not increase the count");

        Assert.That(cache.TryGet(5, out var entry4), Is.True);
        Assert.That(entry4, Is.EqualTo(entry3));

        CacheEntry<int> entry5 = new(30);
        cache.Set(10, entry5);
        Assert.That(cache.Count, Is.EqualTo(2), "Setting a new key should increase the count");

        Assert.That(cache.TryDelete(5), Is.True, "Deleting an existing key should return true");
        Assert.That(cache.Count, Is.EqualTo(1), "Removing an existing key should decrease the count");

        Assert.That(cache.TryGet(5, out _), Is.False, "Getting an unknown key should return false");

        Assert.That(cache.TryDelete(5), Is.False, "Deleting an unknown key should return false");
        Assert.That(cache.Count, Is.EqualTo(1), "Removing an unknown key should not decrease the count");
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

        Assert.That(cache.Count, Is.EqualTo(100));
    }
}