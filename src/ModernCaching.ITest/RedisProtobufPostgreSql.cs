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

namespace ModernCaching.ITest;

/// <summary>
/// - local cache: builtin MemoryCache
/// - distributed cache: Redis with Protobuf serialization
/// - data source: PostgreSQL
/// </summary>
public class RedisProtobufPostgreSql
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

        var redis = await ConnectionMultiplexer.ConnectAsync("localhost,allowAdmin=true");

        string postgreSqlConnectionString = "Host=localhost;User ID=postgres";
        await InitializePostgreSql(postgreSqlConnectionString);

        _cache = await new ReadOnlyCacheBuilder<Guid, User>(new ReadOnlyCacheOptions("test", TimeSpan.FromHours(1)))
            .WithLocalCache(new MemoryCache<Guid, User>())
            .WithDistributedCache(new RedisAsyncCache(redis), new UserSerializer())
            .WithDataSource(new UserDataSource(postgreSqlConnectionString))
            .WithLoggerFactory(new ConsoleLoggerFactory())
            .WithPreload(_ => Task.FromResult<IEnumerable<Guid>>(new Guid[] { new("c11f0067-ec91-4355-8c7e-1caf4c940136")}), null)
            .BuildAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        // ReSharper disable once AccessToDisposedClosure
        await Task.WhenAll(_containerIds.Select(id => KillContainerAsync(_docker!, id)));
        _docker!.Dispose();
    }

    [Test]
    public void PreloadedKeyShouldPresentInCache()
    {
        Guid userId = new("c11f0067-ec91-4355-8c7e-1caf4c940136");
        Assert.That(_cache.TryPeek(userId, out User? user), Is.True);
        Assert.That(user!.Name, Is.EqualTo("Raphaël"));
    }

    [Test]
    public async Task TryPeekShouldRefreshKeyInBackground()
    {
        Guid userId = new("cb22ff11-4683-4ec3-b212-7f1d0ab378cc");
        Assert.That(_cache.TryPeek(userId, out User? user), Is.False);
        do
        {
            await Task.Delay(1000);
        } while (!_cache.TryPeek(userId, out user));
        Assert.That(user, Is.Not.Null);
        Assert.That(user.Name, Is.EqualTo("Gabriel"));
    }

    [Test]
    public async Task TryGetAsyncShouldReturnFalseForNonExistingKey()
    {
        Guid userId = new("cd288523-a602-47d2-ad6b-ceff590fcda9");
        (bool found, User? _) = await _cache.TryGetAsync(userId);
        Assert.That(found, Is.False);
    }

    [Test]
    public async Task TryGetAsyncShouldReturnTrueForExistingKey()
    {
        Guid userId = new("ed67d23e-74bc-42ca-bc6d-ee9f302b67c1");
        (bool found, User? user) = await _cache.TryGetAsync(userId);
        Assert.That(found, Is.True);
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Name, Is.EqualTo("Léo"));
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
        private static readonly TimeSpan HardTimeToLive = TimeSpan.FromDays(7);

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
                    ? new AsyncCacheResult(AsyncCacheStatus.Hit, (byte[])res!)
                    : new AsyncCacheResult(AsyncCacheStatus.Miss, null);
            }
            catch (Exception)
            {
                return new AsyncCacheResult(AsyncCacheStatus.Error, null);
            }
        }

        public Task SetAsync(string key, ReadOnlyMemory<byte> value)
        {
            return _database.StringSetAsync(key, value, HardTimeToLive);
        }

        public Task DeleteAsync(string key)
        {
            return _database.KeyDeleteAsync(key);
        }
    }

    private class UserSerializer : IKeyValueSerializer<Guid, User>
    {
        // Protobuf is forward/backward-compatible so bumping the version shouldn't be needed.
        public int Version => 0;
        public string SerializeKey(Guid key) => key.ToString();
        public Guid DeserializeKey(string keyStr) => Guid.Parse(keyStr);
        public void SerializeValue(User value, Stream stream) => Serializer.Serialize(stream, value);
        public User DeserializeValue(Stream stream) => Serializer.Deserialize<User>(stream);
    }

    private class UserDataSource : IDataSource<Guid, User>
    {
        private readonly string _connectionString;

        public UserDataSource(string connectionString) => _connectionString = connectionString;

        public async IAsyncEnumerable<KeyValuePair<Guid, User>> LoadAsync(IEnumerable<Guid> ids,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            // If there is a lot of keys there may be better alternatives than IN operator.
            string idsStr = '\'' + string.Join("', '", ids) + '\'';
            await using var cmd = new NpgsqlCommand($"SELECT * FROM users WHERE id IN ({idsStr});", conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                Guid id = reader.GetGuid(0);
                string name = reader.GetString(1);
                User user = new() { Id = id, Name = name };
                yield return new KeyValuePair<Guid, User>(id, user);
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
