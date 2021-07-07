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
            Mock<IDataSource<string, string>> dataSourceMock = new();
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Returns(CreateDataSourceResults(new DataSourceResult<string, string>("1", "1111", TimeSpan.FromMilliseconds(1))));

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedDataSource<string, string> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
            await foreach (var _ in instrumentedDataSource.LoadAsync(new[] { "1", "2" }, CancellationToken.None))
            {
            }

            metricsMock.Verify(m => m.IncrementDataSourceLoadOk(), Times.Once);
            metricsMock.Verify(m => m.IncrementDataSourceKeyLoadHits(1), Times.Once);
            metricsMock.Verify(m => m.IncrementDataSourceKeyLoadMisses(1), Times.Once);
        }

        [Test]
        public void LoadAsyncShouldEmitMetricIfItThrows()
        {
            Mock<IDataSource<string, string>> dataSourceMock = new();
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Throws<Exception>();

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedDataSource<string, string> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
            Assert.ThrowsAsync<Exception>(() =>
                instrumentedDataSource.LoadAsync(Array.Empty<string>(), CancellationToken.None)
                    .GetAsyncEnumerator()
                    .MoveNextAsync().AsTask());

            metricsMock.Verify(m => m.IncrementDataSourceLoadError(), Times.Once);
        }

        [Test]
        public void LoadAsyncShouldEmitMetricIfItReturnsNull()
        {
            Mock<IDataSource<string, string>> dataSourceMock = new();
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Returns((IAsyncEnumerable<DataSourceResult<string, string>>)null!);

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedDataSource<string, string> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
            Assert.ThrowsAsync<NullReferenceException>(() =>
                instrumentedDataSource.LoadAsync(Array.Empty<string>(), CancellationToken.None)
                    .GetAsyncEnumerator()
                    .MoveNextAsync().AsTask());

            metricsMock.Verify(m => m.IncrementDataSourceLoadError(), Times.Once);
        }

        [Test]
        public void LoadAsyncShouldEmitMetricForBadKeyResult()
        {
            Mock<IDataSource<string, string>> dataSourceMock = new();
            dataSourceMock
                .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .Returns(CreateDataSourceResults(new DataSourceResult<string, string>?[]
                {
                    null, // Null result.
                    new(null!, "1111", TimeSpan.FromMilliseconds(1)), // Null key.
                    new("XXXXXXX", "2222", TimeSpan.FromMilliseconds(1)), // Key not requested.
                    new("3", "3333", TimeSpan.FromMilliseconds(-1)), // Negative ttl.
                }!));

            Mock<ICacheMetrics> metricsMock = new();

            InstrumentedDataSource<string, string> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
            instrumentedDataSource.LoadAsync(new[] { "1", "2", "3" }, CancellationToken.None)
                .GetAsyncEnumerator()
                .MoveNextAsync();

            metricsMock.Verify(m => m.IncrementDataSourceLoadOk(), Times.Once);
            metricsMock.Verify(m => m.IncrementDataSourceKeyLoadErrors(4), Times.Once);
        }

        private async IAsyncEnumerable<DataSourceResult<string, string>> CreateDataSourceResults(params DataSourceResult<string, string>[] results)
        {
            foreach (var result in results)
            {
                yield return result;
            }
        }
    }
}
