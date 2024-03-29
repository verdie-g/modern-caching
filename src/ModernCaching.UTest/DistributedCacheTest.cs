﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest;

public class DistributedCacheTest
{
    [TestCase(AsyncCacheStatus.Miss)]
    [TestCase(AsyncCacheStatus.Error)]
    public async Task GetAsyncShouldForwardAsyncCacheStatusIsMissOrError(AsyncCacheStatus status)
    {
        AsyncCacheResult asyncCacheResult = new(status, null);
        Mock<IAsyncCache> asyncCacheMock = new(MockBehavior.Strict);
        asyncCacheMock.Setup(c => c.GetAsync("c|0/1|10")).ReturnsAsync(asyncCacheResult);

        Mock<IKeyValueSerializer<int, int>> keyValueSerializerMock = new(MockBehavior.Strict);
        keyValueSerializerMock.Setup(s => s.Version).Returns(1);
        keyValueSerializerMock.Setup(s => s.SerializeKey(10)).Returns("10");

        DistributedCache<int, int> distributedCache = new("c", asyncCacheMock.Object,
            keyValueSerializerMock.Object, null);

        Assert.That(await distributedCache.GetAsync(10), Is.EqualTo((status, null as CacheEntry<int>)));
    }

    [Test]
    public async Task GetAsyncShouldReturnErrorIfAsyncCacheThrows()
    {
        Mock<IAsyncCache> asyncCacheMock = new(MockBehavior.Strict);
        asyncCacheMock.Setup(c => c.GetAsync("c|0/1|10")).ThrowsAsync(new Exception());

        Mock<IKeyValueSerializer<int, int>> keyValueSerializerMock = new(MockBehavior.Strict);
        keyValueSerializerMock.Setup(s => s.Version).Returns(1);
        keyValueSerializerMock.Setup(s => s.SerializeKey(10)).Returns("10");

        DistributedCache<int, int> distributedCache = new("c", asyncCacheMock.Object,
            keyValueSerializerMock.Object, null);

        Assert.That(await distributedCache.GetAsync(10), Is.EqualTo((AsyncCacheStatus.Error, null as CacheEntry<int>)));
    }

    [TestCase(true, 22)]
    [TestCase(true, null)]
    [TestCase(false, null)]
    public async Task GetAfterSetShouldGiveBackInitialValue(bool entryHasValue, int? value)
    {
        DictionaryAsyncCache asyncCache = new();
        IntToIntKeyValueSerializer serializer = new();

        DistributedCache<int, int?> distributedCache = new("c", asyncCache, serializer, null);

        var entry = entryHasValue
            ? new CacheEntry<int?>(value)
            : new CacheEntry<int?>();
        entry.CreationTime = DateTime.UtcNow;
        entry.TimeToLive = TimeSpan.FromHours(1);
        await distributedCache.SetAsync(10, entry);

        var res = await distributedCache.GetAsync(10);
        Assert.That(res.status, Is.EqualTo(AsyncCacheStatus.Hit));
        Assert.That(entry.HasValue, Is.EqualTo(entryHasValue));
        Assert.That(res.entry!.GetValueOrDefault(), Is.EqualTo(entry.GetValueOrDefault()));
        Assert.That(res.entry.CreationTime.Ticks, Is.EqualTo(entry.CreationTime.Ticks).Within(TimeSpan.FromSeconds(1).Ticks));
        Assert.That(entry.CreationTime.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(res.entry.TimeToLive.TotalSeconds, Is.EqualTo(entry.TimeToLive.TotalSeconds).Within(TimeSpan.FromSeconds(1).Ticks));
    }

    private class IntToIntKeyValueSerializer : IKeyValueSerializer<int, int?>
    {
        public int Version => 1;
        public string SerializeKey(int key) => key.ToString();
        public int DeserializeKey(string keyStr) => int.Parse(keyStr);

        public void SerializeValue(int? value, Stream stream)
        {
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(value.HasValue);
            if (value.HasValue)
            {
                writer.Write(value.Value);
            }
        }

        public int? DeserializeValue(Stream stream)
        {
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
            bool hasValue = reader.ReadBoolean();
            return hasValue ? reader.ReadInt32() : null;
        }
    }

    private class DictionaryAsyncCache : IAsyncCache
    {
        private readonly Dictionary<string, byte[]> _dictionary;

        public DictionaryAsyncCache() => _dictionary = new Dictionary<string, byte[]>();

        public Task<AsyncCacheResult> GetAsync(string key)
        {
            return Task.FromResult(_dictionary.TryGetValue(key, out byte[]? value)
                ? new AsyncCacheResult(AsyncCacheStatus.Hit, value)
                : new AsyncCacheResult(AsyncCacheStatus.Miss, value));
        }

        public Task SetAsync(string key, ReadOnlyMemory<byte> value)
        {
            _dictionary[key] = value.ToArray();
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key)
        {
            _dictionary.Remove(key);
            return Task.CompletedTask;
        }
    }
}
