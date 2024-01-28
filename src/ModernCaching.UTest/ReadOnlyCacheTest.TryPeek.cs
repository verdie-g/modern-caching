using System;
using ModernCaching.LocalCaching;
using ModernCaching.Utils;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class ReadOnlyCacheTest_TryPeek
{
    private const string C = "cache_test";
    private static readonly ReadOnlyCacheOptions Options = new();
#pragma warning disable NUnit1032
    private static readonly ITimer Timer = Mock.Of<ITimer>();
#pragma warning restore NUnit1032
    private static readonly IDateTime MachineDateTime = new CachedDateTime(Timer);
    private static readonly CacheEntry<int> FreshEntry = new(10)
    {
        CreationTime = DateTime.UtcNow,
        TimeToLive = TimeSpan.FromHours(1),
    };
    private static readonly CacheEntry<int> FreshEmptyEntry = new()
    {
        CreationTime = DateTime.UtcNow,
        TimeToLive = TimeSpan.FromHours(1),
    };
    private static readonly CacheEntry<int> StaleEntry = new(10)
    {
        CreationTime = DateTime.UtcNow.AddHours(-5),
        TimeToLive = TimeSpan.FromHours(1),
    };

    [Test]
    public void GettingNullKeyShouldThrow()
    {
        ReadOnlyCache<string, string> cache = new(C, null, null, null!, Options, Timer, MachineDateTime, Random.Shared);
        Assert.Throws<ArgumentNullException>(() => cache.TryPeek(null!, out _));
    }

    [Theory]
    public void ShouldReturnLocalEntryIfExists(bool entryHasValue)
    {
        var entry = entryHasValue ? FreshEntry : FreshEmptyEntry;

        Mock<ICache<int, int>> localCacheMock = new();
        localCacheMock
            .Setup(c => c.TryGet(5, out entry))
            .Returns(true);

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, null, null!, Options, Timer, MachineDateTime,
            Random.Shared);
        bool found = cache.TryPeek(5, out int val);
        if (entryHasValue)
        {
            Assert.That(found, Is.True);
            Assert.That(val, Is.EqualTo(10));
        }
        else
        {
            Assert.That(found, Is.False);
            Assert.That(val, Is.Zero);
        }
    }

    [Test]
    public void ShouldReturnLocalEntryEvenIfStale()
    {
        CacheEntry<int>? entry = StaleEntry;

        Mock<ICache<int, int>> localCacheMock = new();
        localCacheMock
            .Setup(c => c.TryGet(5, out entry))
            .Returns(true);

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, null, null!, Options, Timer, MachineDateTime,
            Random.Shared);
        Assert.That(cache.TryPeek(5, out int val), Is.True);
        Assert.That(val, Is.EqualTo(10));
    }

    [Test]
    public void ShouldReturnDefaultIfKeyWasNotPresentInLocalCache()
    {
        CacheEntry<int>? entry = null;

        Mock<ICache<int, int>> localCacheMock = new();
        localCacheMock
            .Setup(c => c.TryGet(5, out entry))
            .Returns(false);

        ReadOnlyCache<int, int> cache = new(C, localCacheMock.Object, null, null!, Options, Timer, MachineDateTime,
            Random.Shared);
        Assert.That(cache.TryPeek(5, out int val), Is.False);
        Assert.That(val, Is.Zero);
    }

    [Test]
    public void ShouldReturnNullIfNullWasCached()
    {
        CacheEntry<object?>? entry = new(null)
        {
            CreationTime = DateTime.Now,
            TimeToLive = TimeSpan.FromHours(1),
        };

        Mock<ICache<int, object?>> localCacheMock = new();
        localCacheMock
            .Setup(c => c.TryGet(5, out entry))
            .Returns(true);

        ReadOnlyCache<int, object?> cache = new(C, localCacheMock.Object, null, null!, Options, Timer,
            MachineDateTime, Random.Shared);
        Assert.That(cache.TryPeek(5, out object? val), Is.True);
        Assert.That(val, Is.Null);
    }
}