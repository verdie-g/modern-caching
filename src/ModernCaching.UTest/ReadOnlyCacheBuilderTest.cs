using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
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
                    .BuildAsync()
            );
        }
    }
}
