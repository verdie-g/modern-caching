using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest
{
    public class ReadOnlyCacheBuilderTest
    {
        [Test]
        public void FullBuilderShouldNotThrow()
        {
            Assert.DoesNotThrowAsync(() =>
                new ReadOnlyCacheBuilder<string, IPEndPoint>("test", Mock.Of<IDataSource<string, IPEndPoint>>())
                    .WithLocalCache(Mock.Of<ICache<string, IPEndPoint>>())
                    .WithDistributedCache(Mock.Of<IAsyncCache>(), Mock.Of<IKeyValueSerializer<string, IPEndPoint>>())
                    .WithLoggerFactory(new NullLoggerFactory())
                    .BuildAsync()
            );
        }
    }
}
