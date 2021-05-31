﻿using System;
using ModernCaching.Instrumentation;
using ModernCaching.LocalCaching;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class InstrumentedCacheTest
    {
        [Test]
        public void TryGetShouldEmitMetricOnHit()
        {
            CacheEntry<int>? entry = new(0, DateTime.UtcNow);
            Mock<ICache<int, int>> cacheMock = new();
            cacheMock.Setup(c => c.TryGet(0, out entry)).Returns(true);

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedCache<int, int> instrumentedCache = new(cacheMock.Object, metricsMock.Object);
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

            InstrumentedCache<int, int> instrumentedCache = new(cacheMock.Object, metricsMock.Object);
            instrumentedCache.TryGet(0, out _);
            metricsMock.Verify(m => m.IncrementLocalCacheGetMisses(), Times.Once);
        }

        [Test]
        public void SetShouldEmitMetric()
        {
            Mock<ICache<int, int>> cacheMock = new();
            cacheMock.Setup(c => c.Set(0, It.IsAny<CacheEntry<int>>()));

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedCache<int, int> instrumentedCache = new(cacheMock.Object, metricsMock.Object);
            instrumentedCache.Set(0, new CacheEntry<int>(1, DateTime.UtcNow));
            metricsMock.Verify(m => m.IncrementLocalCacheSet(), Times.Once);
        }

        [Test]
        public void RemoveShouldEmitMetric()
        {
            Mock<ICache<int, int>> cacheMock = new();
            cacheMock.Setup(c => c.Remove(0));

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedCache<int, int> instrumentedCache = new(cacheMock.Object, metricsMock.Object);
            instrumentedCache.Remove(0);
            metricsMock.Verify(m => m.IncrementLocalCacheRemove(), Times.Once);
        }
    }
}