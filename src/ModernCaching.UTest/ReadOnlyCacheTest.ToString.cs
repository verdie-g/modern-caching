using System;
using ModernCaching.Utils;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class ReadOnlyCacheTest_ToString
{
    [Test]
    public void ToStringShouldReturnCacheName()
    {
        ReadOnlyCache<int, int> cache = new(new ReadOnlyCacheOptions("a", TimeSpan.MaxValue), null, null, null!,
            Mock.Of<ITimer>(), Mock.Of<IDateTime>(), Mock.Of<Random>());
        Assert.That(cache.ToString(), Is.EqualTo("a"));
    }
}