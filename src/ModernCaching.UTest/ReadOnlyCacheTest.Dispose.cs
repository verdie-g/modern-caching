using ModernCaching.Instrumentation;
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
        ReadOnlyCache<int, int> cache = new(C, null, null, null!, new ReadOnlyCacheOptions(), Mock.Of<ITimer>(),
            Mock.Of<IDateTime>(), Mock.Of<IRandom>());
        Assert.DoesNotThrow(() => cache.Dispose());
    }
}