﻿using ModernCaching.Instrumentation;
using ModernCaching.Utils;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class ReadOnlyCacheTest_Dispose
    {
        [Test]
        public void DisposeShouldNotThrow()
        {
            ReadOnlyCache<int, int> cache = new(null, null, null!, Mock.Of<ITimer>(),
                Mock.Of<IDateTime>(), Mock.Of<IRandom>());
            Assert.DoesNotThrow(() => cache.Dispose());
        }
    }
}
