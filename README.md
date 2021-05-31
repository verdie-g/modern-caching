# ModernCaching

A 2-layer, performant and predicable caching solution for modern .NET.

A typical cache provided by this library consists of:
- a synchronous local cache that implements [`ICache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/LocalCaching/ICache.cs)
- an asynchronous distributed cache that implements [`IAsyncCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IAsyncCache.cs)
  (e.g. memcache, redis)
- a data source that implements [`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs)
  (e.g. relational database, Web API, ...)

These 3 components form an [`IReadOnlyCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/IReadOnlyCache.cs).
The 2 cache layers are populated from the
[`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs)
with a backfilling mechanism when getting a value or by preloading some data
when building the cache.

ModernCaching doesn't provide implementations of
[`IAsyncCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IAsyncCache.cs)
or [`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs)
because they are usually tied to the business. Only a single implementation of
[`ICache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/LocalCaching/ICache.cs)
is built-in: 
[`InMemoryCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/LocalCaching/InMemoryCache.cs).

## Features

- **Strict API**. [`IReadOnlyCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/IReadOnlyCache.cs)
  has only two methods:
  - `TryPeek`, a synchronous operation to only get the value if it's present in
    the local cache.
  - `TryGetAsync`, an asynchronous operation to get the first fresh value in the
    local cache, distributed cache or the data source, in that order.
- **Performance**. Unlike other caching libraries that use a `string` as a key or an
  `object` as value or both, `ModernCaching` uses a generic key and value. That
  way, getting a value from the local cache doesn't require any allocation for
  simple type keys such as `int` or more complex user-defined objects. See the
  [benchmarks](https://github.com/verdie-g/modern-caching#benchmarks).
- **Predictability**. Since the number of layers is fixed, it's easy to define
  what should be done when one of these layers is down. For instance, the
  data source is skipped if the distributed cache is unavailable to avoid
  DDOSing it.
- **Instrumentation**. Metrics are exposed through
  [EventCounters](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/event-counters).
  Errors from user-code are logged if a logger is specified.

## Example

This example caches a mapping of "external id" to "internal id". The first
layer is implemented with an in-memory cache, the second one is a memcache
where we specify how to create the key and how to serialize the value using the
interface `IKeyValueSerializer`. Behind these two layers stands the `IDataSource`,
here it's an SQL Server.

```csharp
var cache = await new ReadOnlyCacheBuilder<int, int?>("external_to_internal_id_cache", new ExternalToInternalIdDataSource())
    .WithLocalCache(new InMemoryCache<int, int?>())
    .WithDistributedCache(new MemcacheAsyncCache(), new ExternalToInternalIdKeyValueSerializer())
    .WithPreload(_ => Task.FromResult<IEnumerable<int>>(new[] { 1, 2, 3 }), null)
    .BuildAsync();

int externalId = 5;
bool found = cache.TryPeek(externalId, out int? internalId); // Only check local cache.
(bool found, int? internalId) res = await cache.TryGetAsync(externalId); // Check all layers.
```

<details>
  <summary>See definitions of each layer</summary>

  ```csharp
  class ExternalToInternalIdDataSource : IDataSource<int, int?>
  {
      public async IAsyncEnumerable<DataSourceResult<int, int?>> LoadAsync(IEnumerable<int> keys,
          [EnumeratorCancellation] CancellationToken cancellationToken)
      {
          await using SqlConnection connection = new("Data Source=(local)");
          string keysStr = "(" + string.Join("),(", keys) + ")";
          SqlCommand command = new(@$"
              SELECT u.external_id, u.internal_id
              FROM (VALUES {keysStr}) as input(external_id)
              LEFT JOIN users u ON input.external_id = u.external_id",
              connection);
          await connection.OpenAsync(cancellationToken);
          SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
          while (await reader.ReadAsync(cancellationToken))
          {
              int externalId = reader.GetInt32(0);
              int? internalId = reader[1] as int?;
              yield return new DataSourceResult<int, int?>(externalId, internalId, TimeSpan.FromHours(1));
          }
          await reader.CloseAsync();
      }
  }

  class ExternalToInternalIdKeyValueSerializer : IKeyValueSerializer<int, int?>
  {
      public int Version => 0; // Bump after making breaking change in the serialization.
      public string StringifyKey(int key) => key.ToString();
      public void SerializeValue(int? value, BinaryWriter writer)
      {
          if (value.HasValue) writer.Write(value.Value);
      }
      public int? DeserializeValue(ReadOnlySpan<byte> valueBytes)
      {
          return valueBytes.IsEmpty ? null : BitConverter.ToInt32(valueBytes);
      }
  }
  ```
</details>

## Benchmarks

Benchmark of the very hot path of different caching libraries, that is,
getting locally cached data.

|        Method |     Mean |    Error |   StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------- |---------:|---------:|---------:|-------:|------:|------:|----------:|
| ModernCaching | 115.7 ns |  2.32 ns |  5.99 ns |      - |     - |     - |         - |
|    CacheTower | 132.0 ns |  2.59 ns |  3.27 ns | 0.0202 |     - |     - |      32 B |
|   FusionCache | 407.8 ns |  8.09 ns | 20.14 ns | 0.0351 |     - |     - |      56 B |
|   EasyCaching | 545.7 ns | 10.67 ns | 19.52 ns | 0.1274 |     - |     - |     200 B |

Since this library has a generic interface, getting a value doesn't involve any
allocation.

Code can be found in [src/ModernCaching.Benchmarks](https://github.com/verdie-g/modern-caching/tree/main/src/ModernCaching.Benchmarks).

## Instrumentation

### Metrics

Metrics are exposed using the standard way for modern .NET:
[EventCounters](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/event-counters).
See the following example where the metrics are collected using
[dotnet-counters](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters).

![metrics](https://user-images.githubusercontent.com/9092290/120233516-41b09680-c256-11eb-9088-cc4a033920fe.gif)

### Logs

Use `WithLoggerFactory` on the builder to log all user-code errors coming from
[`IAsyncCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IAsyncCache.cs),
[`IKeyValueSerializer`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IKeyValueSerializer.cs) or
[`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs).

## License

All code found in this repository is licensed under MIT. See the
[LICENSE](https://github.com/verdie-g/crpg/blob/master/LICENSE)
file in the project root for the full license text.
