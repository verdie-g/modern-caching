using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using ModernCaching.DataSource;
using ModernCaching.DistributedCaching;
using ModernCaching.LocalCaching;
using Npgsql;
using NUnit.Framework;
using ProtoBuf;
using StackExchange.Redis;

namespace ModernCaching.ITest
{
    public class RedisPostgreSql
    {
        private DockerClient? _docker;
        private string[] _containerIds = Array.Empty<string>();
        private IReadOnlyCache<Guid, User> _cache = default!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _docker = new DockerClientConfiguration().CreateClient();
            _containerIds = await Task.WhenAll(
                RunContainerAsync(_docker, "redis", 6379),
                RunContainerAsync(_docker, "postgres", 5432, new[] { "POSTGRES_HOST_AUTH_METHOD=trust" })
            );

            var redis = await ConnectionMultiplexer.ConnectAsync("localhost");
            string postgreSqlConnectionString = "Host=localhost;User ID=postgres";
            await InitializePostgreSql(postgreSqlConnectionString);

            _cache = await new ReadOnlyCacheBuilder<Guid, User>("test", new UserDataSource(postgreSqlConnectionString))
                .WithLocalCache(new MemoryCache<Guid, User>())
                .WithDistributedCache(new RedisAsyncCache(redis), new ProtobufKeyValueSerializer<Guid, User>())
                .WithLoggerFactory(new ConsoleLoggerFactory())
                .BuildAsync();
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await Task.WhenAll(_containerIds.Select(id => KillContainerAsync(_docker!, id)));
        }

        [Test]
        public async Task TryPeekShouldReloadKeyInBackground()
        {
            Guid userId = new("cb22ff11-4683-4ec3-b212-7f1d0ab378cc");
            Assert.IsFalse(_cache.TryPeek(userId, out User? user));
            do
            {
                await Task.Delay(1000);
            } while (!_cache.TryPeek(userId, out user));
            Assert.IsNotNull(user);
            Assert.AreEqual("Gabriel", user!.Name);
        }

        [Test]
        public async Task TryGetAsyncShouldReturnFalseForNonExistingKey()
        {
            Guid userId = new("cd288523-a602-47d2-ad6b-ceff590fcda9");
            (bool found, User? _) = await _cache.TryGetAsync(userId);
            Assert.IsFalse(found);
        }

        [Test]
        public async Task TryGetAsyncShouldReturnTrueForExistingKey()
        {
            Guid userId = new("ed67d23e-74bc-42ca-bc6d-ee9f302b67c1");
            (bool found, User? user) = await _cache.TryGetAsync(userId);
            Assert.IsTrue(found);
            Assert.IsNotNull(user);
            Assert.AreEqual("Léo", user!.Name);
        }

        private static async Task InitializePostgreSql(string connectionString)
        {
            await using var conn = new NpgsqlConnection(connectionString);
            for (int i = 0; i < 3; i += 1)
            {
                try
                {
                    await conn.OpenAsync();
                    break;
                }
                catch (NpgsqlException) when (i < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            await using var cmd = new NpgsqlCommand(@"
CREATE TABLE users (
  id UUID PRIMARY KEY,
  name TEXT NOT NULL
);

INSERT INTO users VALUES
  ('cb22ff11-4683-4ec3-b212-7f1d0ab378cc', 'Gabriel'),
  ('ed67d23e-74bc-42ca-bc6d-ee9f302b67c1', 'Léo'),
  ('c11f0067-ec91-4355-8c7e-1caf4c940136', 'Raphaël'),
  ('0284bec7-26ef-4039-bbed-28e5e9e62851', 'Arthur'),
  ('841c6853-c833-4d37-ba0d-7d492da4b440', 'Louis'),
  ('ca1fe180-5f4c-4fa4-8b8a-87ffe043faba', 'Emma'),
  ('88683f66-4905-4d8e-93a1-e47eb29ee25b', 'Jade'),
  ('53b1269f-f318-4787-af37-14d509e1d664', 'Louise'),
  ('03e504a7-8bd4-4e0f-833f-483c3d542e34', 'Lucas');", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<string> RunContainerAsync(DockerClient docker, string image, int port, IList<string>? env = null)
        {
            await docker.Images.CreateImageAsync(new ImagesCreateParameters
            {
                FromImage = image,
                Tag = "latest",
            }, null, new Progress<JSONMessage>(m => TestContext.WriteLine(m.Status)));

            var container = await docker.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = image,
                Env = env,
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        [port + "/tcp"] = new PortBinding[]
                        {
                            new() { HostIP = IPAddress.Loopback.ToString(), HostPort = port.ToString() },
                        },
                    },
                },
            });

            await docker.Containers.StartContainerAsync(container.ID, new ContainerStartParameters());
            return container.ID;
        }

        private static Task KillContainerAsync(DockerClient docker, string containerId)
        {
            return docker.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
            {
                RemoveVolumes = true,
                Force = true,
            });
        }

        private class RedisAsyncCache : IAsyncCache
        {
            private readonly IDatabase _database;

            public RedisAsyncCache(IConnectionMultiplexer redis)
            {
                _database = redis.GetDatabase();
            }

            public async Task<AsyncCacheResult> GetAsync(string key)
            {
                try
                {
                    var res = await _database.StringGetAsync(key);
                    return res.HasValue
                        ? new AsyncCacheResult(AsyncCacheStatus.Hit, (byte[])res)
                        : new AsyncCacheResult(AsyncCacheStatus.Miss, null);
                }
                catch (Exception)
                {
                    return new AsyncCacheResult(AsyncCacheStatus.Error, null);
                }
            }

            public Task SetAsync(string key, byte[] value, TimeSpan timeToLive)
            {
                return _database.StringSetAsync(key, value, timeToLive);
            }

            public Task DeleteAsync(string key)
            {
                return _database.KeyDeleteAsync(key);
            }
        }

        private class ProtobufKeyValueSerializer<TKey, TValue> : IKeyValueSerializer<TKey, TValue> where TKey : notnull
        {
            public int Version => 0;
            public string StringifyKey(TKey key) => key.ToString()!;
            public void SerializeValue(TValue value, BinaryWriter writer) => Serializer.Serialize(writer.BaseStream, value);
            public TValue DeserializeValue(BinaryReader reader) => Serializer.Deserialize<TValue>(reader.BaseStream);
        }

        private class UserDataSource : IDataSource<Guid, User>
        {
            private readonly string _connectionString;

            public UserDataSource(string connectionString) => _connectionString = connectionString;

            public async IAsyncEnumerable<DataSourceResult<Guid, User>> LoadAsync(IEnumerable<Guid> ids,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                string idsStr = '\'' + string.Join("', '", ids) + '\'';
                await using var cmd = new NpgsqlCommand($"SELECT * FROM users WHERE id IN ({idsStr});", conn);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    Guid id = reader.GetGuid(0);
                    string name = reader.GetString(1);
                    User user = new() { Id = id, Name = name };
                    yield return new DataSourceResult<Guid, User>(id, user, TimeSpan.FromHours(1));
                }
            }
        }

        [ProtoContract]
        private record User
        {
            [ProtoMember(1)]
            public Guid Id { get; init; }

            [ProtoMember(2)]
            public string Name { get; init; } = string.Empty;
        }
    }
}
