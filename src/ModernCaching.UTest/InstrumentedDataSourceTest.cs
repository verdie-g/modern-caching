using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.DataSource;
using ModernCaching.Instrumentation;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class InstrumentedDataSourceTest
    {
        [Test]
        public async Task LoadAsyncShouldEmitMetricIfItDoesntThrow()
        {
            Mock<IDataSource<int, int>> dataSourceMock = new();
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
                .Returns(CreateDataSourceResults());

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedDataSource<int, int> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
            await foreach (var _ in instrumentedDataSource.LoadAsync(Array.Empty<int>(), CancellationToken.None))
            {
            }

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

            InstrumentedDataSource<int, int> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
            Assert.ThrowsAsync<Exception>(() =>
                instrumentedDataSource.LoadAsync(Array.Empty<int>(), CancellationToken.None)
                    .GetAsyncEnumerator()
                    .MoveNextAsync().AsTask());

            metricsMock.Verify(m => m.IncrementDataSourceLoadError(), Times.Once);
        }

        [Test]
        public void LoadAsyncShouldEmitMetricIfItReturnsNull()
        {
            Mock<IDataSource<int, int>> dataSourceMock = new();
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
                .Returns((IAsyncEnumerable<DataSourceResult<int, int>>)null!);

            Mock<ICacheMetrics> metricsMock = new();


            InstrumentedDataSource<int, int> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
            Assert.ThrowsAsync<NullReferenceException>(() =>
                instrumentedDataSource.LoadAsync(Array.Empty<int>(), CancellationToken.None)
                    .GetAsyncEnumerator()
                    .MoveNextAsync().AsTask());

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
