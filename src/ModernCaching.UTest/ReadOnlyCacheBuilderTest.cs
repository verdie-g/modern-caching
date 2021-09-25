using System;
using System.Net;
using System.Threading.Tasks;
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
        public void ConstructorShouldThrowIfKeyDoesNotOverrideEquals()
        {
            Assert.Throws<ArgumentException>(() =>
                new ReadOnlyCacheBuilder<NoEqualsType, int>("test", Mock.Of<IDataSource<NoEqualsType, int>>())
            );
        }

        [Test]
        public void ConstructorShouldThrowIfKeyDoesNotOverrideGetHashCode()
        {
            Assert.Throws<ArgumentException>(() =>
                new ReadOnlyCacheBuilder<NoGetHashCodeType, int>("test", Mock.Of<IDataSource<NoGetHashCodeType, int>>())
            );
        }

        [Test]
        public void ConstructorShouldNotThrowIfKeyOverridesEqualsAndGetHashCode()
        {
            Assert.DoesNotThrow(() =>
                new ReadOnlyCacheBuilder<OkType, int>("test", Mock.Of<IDataSource<OkType, int>>())
            );
        }

        [Test]
        public void ConstructorShouldNotThrowIfKeyDerivesFromTypeOverridingEqualsAndGetHashCode()
        {
            Assert.DoesNotThrow(() =>
                new ReadOnlyCacheBuilder<DeriveOkType, int>("test", Mock.Of<IDataSource<DeriveOkType, int>>())
            );
        }

        [Test]
        public void ConstructorShouldNotThrowIfKeyIsCommonType()
        {
            static void T<T>() where T : notnull => Assert.DoesNotThrow(() =>
                new ReadOnlyCacheBuilder<T, int>("test", Mock.Of<IDataSource<T, int>>())
            );

            T<sbyte>();
            T<short>();
            T<int>();
            T<long>();
            T<byte>();
            T<ushort>();
            T<uint>();
            T<ulong>();
            T<float>();
            T<double>();
            T<string>();
            T<IPAddress>();
        }

        [Test]
        public async Task CacheNameShouldGetNormalized()
        {
            var cache = await new ReadOnlyCacheBuilder<int, int>("-Ri cé#\t ^k.ro Ll_", Mock.Of<IDataSource<int, int>>())
                .BuildAsync();
            Assert.AreEqual("-Rick.roLl_", cache.ToString());
        }

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

        public class NoEqualsType { public override int GetHashCode() => 0; }
        public class NoGetHashCodeType { public override bool Equals(object? obj) => true; }
        public class OkType { public override bool Equals(object? obj) => true; public override int GetHashCode() => 0; }
        public class DeriveOkType : OkType { }
    }
}
