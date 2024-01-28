using System;
using ModernCaching.Utils;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class ReadOnlyCacheTest_Dispose
{
    private const string C = "cache_test";

    [Test]
    public void DisposeShouldNotThrow()
    {
        ReadOnlyCache<int, int> cache = new(new ReadOnlyCacheOptions("a", TimeSpan.MaxValue), null, null, null!,
            Mock.Of<ITimer>(), Mock.Of<IDateTime>(), Mock.Of<Random>());
        Assert.DoesNotThrow(() => cache.Dispose());
    }
}