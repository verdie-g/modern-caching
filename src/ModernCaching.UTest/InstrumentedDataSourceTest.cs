using System;
using System.Collections.Generic;
using System.Threading;
using ModernCaching.DataSource;
using ModernCaching.Instrumentation;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class InstrumentedDataSourceTest
    {
        [Test]
        public void LoadAsyncShouldEmitMetricIfItDoesntThrow()
        {
            Mock<IDataSource<int, int>> dataSourceMock = new();
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
                .Returns(CreateDataSourceResults());

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedDataSource<int, int> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object);
            instrumentedDataSource.LoadAsync(Array.Empty<int>(), CancellationToken.None);
            metricsMock.Verify(m => m.IncrementDataSourceLoadOk(), Times.Once);
        }

        [Test]
        public void LoadAsyncShouldEmitMetricIfItThrows()
        {
            Mock<IDataSource<int, int>> dataSourceMock = new();
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
                .Throws<Exception>();

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedDataSource<int, int> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object);
            try
            {
                instrumentedDataSource.LoadAsync(Array.Empty<int>(), CancellationToken.None);
            }
            catch
            {
                // ignored
            }

            metricsMock.Verify(m => m.IncrementDataSourceLoadError(), Times.Once);
        }

        private async IAsyncEnumerable<DataSourceResult<int, int>> CreateDataSourceResults(params DataSourceResult<int, int>[] results)
        {
            foreach (var result in results)
            {
                yield return result;
            }
        }
    }
}
