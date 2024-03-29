﻿using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class ReadOnlyCacheBuilderTest
{
    [Test]
    public void ConstructorShouldThrowIfKeyDoesNotOverrideEquals()
    {
        Assert.Throws<ArgumentException>(() =>
            _ = new ReadOnlyCacheBuilder<NoEqualsType, int>(new ReadOnlyCacheOptions("test", TimeSpan.MaxValue))
        );
    }

    [Test]
    public void ConstructorShouldThrowIfKeyDoesNotOverrideGetHashCode()
    {
        Assert.Throws<ArgumentException>(() =>
            _ = new ReadOnlyCacheBuilder<NoGetHashCodeType, int>(new ReadOnlyCacheOptions("test", TimeSpan.MaxValue))
        );
    }

    [Test]
    public void ConstructorShouldNotThrowIfKeyOverridesEqualsAndGetHashCode()
    {
        Assert.DoesNotThrow(() =>
            _ = new ReadOnlyCacheBuilder<OkType, int>(new ReadOnlyCacheOptions("test", TimeSpan.MaxValue))
        );
    }

    [Test]
    public void ConstructorShouldNotThrowIfKeyDerivesFromTypeOverridingEqualsAndGetHashCode()
    {
        Assert.DoesNotThrow(() =>
            _ = new ReadOnlyCacheBuilder<DeriveOkType, int>(new ReadOnlyCacheOptions("test", TimeSpan.MaxValue))
        );
    }

    [Test]
    public void ConstructorShouldNotThrowIfKeyIsCommonType()
    {
        static void T<T>() where T : notnull => Assert.DoesNotThrow(() =>
            _ = new ReadOnlyCacheBuilder<T, int>(new ReadOnlyCacheOptions("test", TimeSpan.MaxValue))
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
    public void BuildAsyncShouldThrowIfNoDataSource()
    {
        Assert.That(() =>
            new ReadOnlyCacheBuilder<string, IPEndPoint>(new ReadOnlyCacheOptions("test", TimeSpan.FromSeconds(1))).BuildAsync(),
            Throws.InvalidOperationException
        );
    }

    [Test]
    public async Task CacheNameShouldGetNormalized()
    {
        var cache = await new ReadOnlyCacheBuilder<int, int>(new ReadOnlyCacheOptions("-Ri cé#\t ^k.ro Ll_", TimeSpan.MaxValue))
            .WithDataSource(Mock.Of<IDataSource<int, int>>())
            .BuildAsync();
        Assert.That(cache.ToString(), Is.EqualTo("-Rick.roLl_"));
    }

    [Test]
    public void FullBuilderShouldNotThrow()
    {
        Assert.DoesNotThrowAsync(() =>
            new ReadOnlyCacheBuilder<string, IPEndPoint>(new ReadOnlyCacheOptions("test", TimeSpan.MaxValue))
                .WithLocalCache(Mock.Of<ICache<string, IPEndPoint>>())
                .WithDistributedCache(Mock.Of<IAsyncCache>(), Mock.Of<IKeyValueSerializer<string, IPEndPoint>>())
                .WithDataSource(Mock.Of<IDataSource<string, IPEndPoint>>())
                .WithLoggerFactory(new NullLoggerFactory())
                .BuildAsync()
        );
    }

    public class NoEqualsType { public override int GetHashCode() => 0; }
#pragma warning disable 659 // For test
    public class NoGetHashCodeType { public override bool Equals(object? obj) => true; }
#pragma warning restore 659
    public class OkType { public override bool Equals(object? obj) => true; public override int GetHashCode() => 0; }
    public class DeriveOkType : OkType { }
}