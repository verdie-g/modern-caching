using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;
using Moq;
using NUnit.Framework;

namespace ModernCaching.UTest
{
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
            keyValueSerializerMock.Setup(s => s.StringifyKey(10)).Returns("10");

            DistributedCache<int, int> distributedCache = new("c", asyncCacheMock.Object,
                keyValueSerializerMock.Object, null, null);

            Assert.AreEqual((status, null as CacheEntry<int>), await distributedCache.GetAsync(10));
        }

        [Test]
        public async Task GetAsyncShouldReturnErrorIfAsyncCacheThrows()
        {
            Mock<IAsyncCache> asyncCacheMock = new(MockBehavior.Strict);
            asyncCacheMock.Setup(c => c.GetAsync("c|0/1|10")).ThrowsAsync(new Exception());

            Mock<IKeyValueSerializer<int, int>> keyValueSerializerMock = new(MockBehavior.Strict);
            keyValueSerializerMock.Setup(s => s.Version).Returns(1);
            keyValueSerializerMock.Setup(s => s.StringifyKey(10)).Returns("10");

            DistributedCache<int, int> distributedCache = new("c", asyncCacheMock.Object,
                keyValueSerializerMock.Object, null, null);

            Assert.AreEqual((AsyncCacheStatus.Error, null as CacheEntry<int>), await distributedCache.GetAsync(10));
        }

        [TestCase(true, 22)]
        [TestCase(true, null)]
        [TestCase(false, null)]
        public async Task GetAfterSetShouldGiveBackInitialValue(bool entryHasValue, int? value)
        {
            DictionaryAsyncCache asyncCache = new();
            IntToIntKeyValueSerializer serializer = new();

            DistributedCache<int, int?> distributedCache = new("c", asyncCache, serializer, "ab", null);

            var entry = entryHasValue
                ? new CacheEntry<int?>(value) { ExpirationTime = DateTime.UtcNow.AddHours(1), EvictionTime = DateTime.UtcNow.AddHours(2) }
                : new CacheEntry<int?> { ExpirationTime = DateTime.UtcNow.AddHours(1), EvictionTime = DateTime.UtcNow.AddHours(2) };
            await distributedCache.SetAsync(10, entry);

            var res = await distributedCache.GetAsync(10);
            Assert.AreEqual(AsyncCacheStatus.Hit, res.status);
            Assert.AreEqual(entryHasValue, entry.HasValue);
            Assert.AreEqual(entry.GetValueOrDefault(), res.entry!.GetValueOrDefault());
            Assert.AreEqual(entry.ExpirationTime.Ticks, res.entry.ExpirationTime.Ticks, TimeSpan.FromSeconds(1).Ticks);
            Assert.AreEqual(DateTimeKind.Utc, entry.ExpirationTime.Kind);
            Assert.AreEqual(entry.EvictionTime.Ticks, res.entry.EvictionTime.Ticks, TimeSpan.FromSeconds(1).Ticks);
            Assert.AreEqual(DateTimeKind.Utc, entry.EvictionTime.Kind);
        }

        private class IntToIntKeyValueSerializer : IKeyValueSerializer<int, int?>
        {
            public int Version => 1;
            public string StringifyKey(int key) => key.ToString();

            public void SerializeValue(int? value, BinaryWriter writer)
            {
                if (value.HasValue)
                {
                    writer.Write(value.Value);
                }
            }

            public int? DeserializeValue(BinaryReader reader)
            {
                return reader.BaseStream.Position < reader.BaseStream.Length
                    ? reader.ReadInt32()
                    : null;
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

            public Task SetAsync(string key, byte[] value, TimeSpan timeToLive)
            {
                _dictionary[key] = value;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(string key)
            {
                _dictionary.Remove(key);
                return Task.CompletedTask;
            }
        }
    }
}
