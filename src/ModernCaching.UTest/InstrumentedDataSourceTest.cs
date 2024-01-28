using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModernCaching.DataSource;
using ModernCaching.Telemetry;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class InstrumentedDataSourceTest
{
    [Test]
    public async Task LoadAsyncShouldEmitMetricIfItDoesntThrow()
    {
        Mock<IDataSource<string, string>> dataSourceMock = new();
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateKeyValuePairs(new KeyValuePair<string, string>("1", "1111")));

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedDataSource<string, string> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
        await foreach (var _ in instrumentedDataSource.LoadAsync(new[] { "1", "2" }, CancellationToken.None))
        {
        }

        metricsMock.Verify(m => m.IncrementDataSourceLoadOks(), Times.Once);
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

        metricsMock.Verify(m => m.IncrementDataSourceLoadErrors(), Times.Once);
    }

    [Test]
    public void LoadAsyncShouldEmitMetricIfItReturnsNull()
    {
        Mock<IDataSource<string, string>> dataSourceMock = new();
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IAsyncEnumerable<KeyValuePair<string, string>>)null!);

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedDataSource<string, string> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
        Assert.ThrowsAsync<NullReferenceException>(() =>
            instrumentedDataSource.LoadAsync(Array.Empty<string>(), CancellationToken.None)
                .GetAsyncEnumerator()
                .MoveNextAsync().AsTask());

        metricsMock.Verify(m => m.IncrementDataSourceLoadErrors(), Times.Once);
    }

    [Test]
    public void LoadAsyncShouldEmitMetricForBadKeyResult()
    {
        Mock<IDataSource<string, string>> dataSourceMock = new();
        dataSourceMock
            .Setup(s => s.LoadAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateKeyValuePairs(new KeyValuePair<string, string>[]
            {
                new(null!, "1111"), // Null key.
                new("XXXXXXX", "2222"), // Key not requested.
            }!));

        Mock<ICacheMetrics> metricsMock = new();

        InstrumentedDataSource<string, string> instrumentedDataSource = new(dataSourceMock.Object, metricsMock.Object, null);
        instrumentedDataSource.LoadAsync(new[] { "1", "2", "3" }, CancellationToken.None)
            .GetAsyncEnumerator()
            .MoveNextAsync();

        metricsMock.Verify(m => m.IncrementDataSourceLoadOks(), Times.Once);
        metricsMock.Verify(m => m.IncrementDataSourceKeyLoadErrors(2), Times.Once);
    }

#pragma warning disable 1998
    private async IAsyncEnumerable<KeyValuePair<string, string>> CreateKeyValuePairs(params KeyValuePair<string, string>[] results)
#pragma warning restore 1998
    {
        foreach (var result in results)
        {
            yield return result;
        }
    }
}