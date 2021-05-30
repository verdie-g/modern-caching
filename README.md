# ModernCaching

A 2-layer, performant and predicable caching solution for modern .NET.

A typical cache provided by this library consists of:
- a synchronous local cache that implements [`ICache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/LocalCaching/ICache.cs)
- an asynchronous distributed cache that implements [`IAsyncCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IAsyncCache.cs)
  (e.g. memcache, redis)
- a data source that implements [`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs)
  (e.g. relational database, Web API, ...)

These 3 components form an [`IReadOnlyCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/IReadOnlyCache.cs).
The 2 cache layers are populated from the `IDataSource` with a backfilling
mechanism when getting a value or by preloading some data when building the cache.

ModernCaching doesn't provide implementations of
[`IAsyncCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IAsyncCache.cs)
or [`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs)
because they are usually tied to the business. Only a single implementation of
[`ICache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/LocalCaching/ICache.cs)
is built-in: 
[`InMemoryCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/LocalCaching/InMemoryCache.cs).


## Features

- Strict API. [`IReadOnlyCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/IReadOnlyCache.cs)
  has only two methods:
  - `TryPeek`, a synchronous operation to only get the value if it's present in
    the local cache.
  - `TryGetAsync`, an asynchronous operation to get the first fresh value in the
    local cache, distributed cache or the data source, in that order.
- Performance. Unlike other caching libraries that use a `string` as a key or an
  `object` as value or both, `ModernCaching` uses a generic key and value. That
  way, getting a value from the local cache doesn't require any allocation for
  simple type keys such as `int` or more complex user-defined objects. See the
  [benchmarks](https://github.com/verdie-g/modern-caching#benchmarks).
- Predictability. Since the number of layers is fixed, it's easy to define
  what should be done when one of these layers is down. For instance, the
  data source is skipped if the distributed cache is unavailable to avoid
  DDOSing it.

## Example

This example caches a mapping of "external id" to "internal id". The first
layer is implemented with an in-memory cache, the second one is a memcache
where we specify how to create the key and how to serialize the value using the
interface `IKeyValueSerializer`. Behind these two layers stands the `IDataSource`
which is usually a relational database but it's up to the implementation.

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

|        Method |     Mean |    Error |  StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------- |---------:|---------:|--------:|-------:|------:|------:|----------:|
| ModernCaching | 104.7 ns |  0.82 ns | 0.69 ns |      - |     - |     - |         - |
|    CacheTower | 125.5 ns |  2.41 ns | 2.48 ns | 0.0202 |     - |     - |      32 B |
|   FusionCache | 384.5 ns |  7.63 ns | 9.36 ns | 0.0355 |     - |     - |      56 B |
|   EasyCaching | 526.4 ns | 10.24 ns | 9.07 ns | 0.1274 |     - |     - |     200 B |

Since this library has a generic interface, getting a value doesn't involve any
allocation.

Code can be found in [src/ModernCaching.Benchmarks](https://github.com/verdie-g/modern-caching/tree/main/src/ModernCaching.Benchmarks).
