using System;
using ModernCaching.Utils;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class ReadOnlyCacheTest_ToString
{
    private const string C = "cache_test";

    [Test]
    public void ToStringShouldReturnCacheName()
    {
        ReadOnlyCache<int, int> cache = new(C, null, null, null!, new ReadOnlyCacheOptions(), Mock.Of<ITimer>(),
            Mock.Of<IDateTime>(), Mock.Of<Random>());
        Assert.That(cache.ToString(), Is.EqualTo(C));
    }
}