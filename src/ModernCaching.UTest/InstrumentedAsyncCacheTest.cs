using System;
using ModernCaching.DistributedCaching;
using ModernCaching.Instrumentation;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class InstrumentedAsyncCacheTest
{
    [Test]
    public void GetAsyncShouldEmitMetricOnHit()
    {
        Mock<IAsyncCache> cacheMock = new();
        cacheMock
            .Setup(c => c.GetAsync("0"))
            .ReturnsAsync(new AsyncCacheResult(AsyncCacheStatus.Hit, Array.Empty<byte>()));

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedAsyncCache instrumentedCache = new(cacheMock.Object, metricsMock.Object, null);
        instrumentedCache.GetAsync("0");
        metricsMock.Verify(m => m.IncrementDistributedCacheGetHits(), Times.Once);
    }

    [Test]
    public void GetAsyncShouldEmitMetricOnMiss()
    {
        Mock<IAsyncCache> cacheMock = new();
        cacheMock
            .Setup(c => c.GetAsync("0"))
            .ReturnsAsync(new AsyncCacheResult(AsyncCacheStatus.Miss, null));

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedAsyncCache instrumentedCache = new(cacheMock.Object, metricsMock.Object, null);
        instrumentedCache.GetAsync("0");
        metricsMock.Verify(m => m.IncrementDistributedCacheGetMisses(), Times.Once);
    }

    [Test]
    public void SetAsyncShouldEmitMetric()
    {
        Mock<IAsyncCache> cacheMock = new();
        cacheMock.Setup(c => c.SetAsync("0", It.IsAny<byte[]>(), It.IsAny<TimeSpan>()));

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedAsyncCache instrumentedCache = new(cacheMock.Object, metricsMock.Object, null);
        instrumentedCache.SetAsync("0", Array.Empty<byte>(), TimeSpan.Zero);
        metricsMock.Verify(m => m.IncrementDistributedCacheSets(), Times.Once);
    }

    [Test]
    public void DeleteAsyncShouldEmitMetric()
    {
        Mock<IAsyncCache> cacheMock = new();
        cacheMock.Setup(c => c.DeleteAsync("0"));

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedAsyncCache instrumentedCache = new(cacheMock.Object, metricsMock.Object, null);
        instrumentedCache.DeleteAsync("0");
        metricsMock.Verify(m => m.IncrementDistributedCacheDeletes(), Times.Once);
    }
}