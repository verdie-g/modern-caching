using System;
using Microsoft.Extensions.Logging;
using ModernCaching.LocalCaching;
using ModernCaching.Telemetry;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class InstrumentedCacheTest
{
    [Test]
    public void CountShouldReturnUnderlyingCount()
    {
        Mock<ICache<int, int>> cacheMock = new();
        cacheMock.Setup(c => c.Count).Returns(5);

        InstrumentedCache<int, int> instrumentedCache = new(cacheMock.Object, Mock.Of<ICacheMetrics>(), Mock.Of<ILogger>());
        Assert.AreEqual(5, instrumentedCache.Count);
    }

    [Test]
    public void TryGetShouldEmitMetricOnHit()
    {
        CacheEntry<int>? entry = new(0) { CreationTime = DateTime.UtcNow, TimeToLive = TimeSpan.Zero };
        Mock<ICache<int, int>> cacheMock = new();
        cacheMock.Setup(c => c.TryGet(0, out entry)).Returns(true);

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedCache<int, int> instrumentedCache = new(cacheMock.Object, metricsMock.Object, Mock.Of<ILogger>());
        instrumentedCache.TryGet(0, out _);
        metricsMock.Verify(m => m.IncrementLocalCacheGetHits(), Times.Once);
    }

    [Test]
    public void TryGetShouldEmitMetricOnMiss()
    {
        CacheEntry<int>? entry = null;
        Mock<ICache<int, int>> cacheMock = new();
        cacheMock.Setup(c => c.TryGet(0, out entry)).Returns(false);

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedCache<int, int> instrumentedCache = new(cacheMock.Object, metricsMock.Object, Mock.Of<ILogger>());
        instrumentedCache.TryGet(0, out _);
        metricsMock.Verify(m => m.IncrementLocalCacheGetMisses(), Times.Once);
    }

    [Test]
    public void SetShouldEmitMetric()
    {
        Mock<ICache<int, int>> cacheMock = new();
        cacheMock.Setup(c => c.Set(0, It.IsAny<CacheEntry<int>>()));

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedCache<int, int> instrumentedCache = new(cacheMock.Object, metricsMock.Object, Mock.Of<ILogger>());
        CacheEntry<int> cacheEntry = new(1) { CreationTime = DateTime.UtcNow, TimeToLive = TimeSpan.Zero };
        instrumentedCache.Set(0, cacheEntry);
        metricsMock.Verify(m => m.IncrementLocalCacheSets(), Times.Once);
    }

    [Test]
    public void DeleteShouldEmitMetric()
    {
        Mock<ICache<int, int>> cacheMock = new();
        cacheMock.Setup(c => c.TryDelete(0)).Returns(true);
        cacheMock.Setup(c => c.TryDelete(1)).Returns(false);

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedCache<int, int> instrumentedCache = new(cacheMock.Object, metricsMock.Object, Mock.Of<ILogger>());
        instrumentedCache.TryDelete(0);
        metricsMock.Verify(m => m.IncrementLocalCacheDeleteHits(), Times.Once);
        instrumentedCache.TryDelete(1);
        metricsMock.Verify(m => m.IncrementLocalCacheDeleteMisses(), Times.Once);
    }
}