# ModernCaching

A 2-layer, performant and resilient caching solution for modern .NET.

A typical cache provided by this library consists of:
- a synchronous local cache that implements [`ICache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/LocalCaching/ICache.cs)
- an asynchronous distributed cache that implements [`IAsyncCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IAsyncCache.cs)
  (e.g. memcache, redis)
- a data source that implements [`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs)
  (e.g. relational database, Web API, CPU intensive task...)

These 3 components form an [`IReadOnlyCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/IReadOnlyCache.cs).
The 2 cache layers are populated from the
[`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs)
with a backfilling mechanism when getting a value or by preloading some data
when building the cache.

![schema](https://user-images.githubusercontent.com/9092290/122583694-d5a59f00-d059-11eb-826b-6fd8011df3b0.png)

ModernCaching doesn't provide implementations of
[`IAsyncCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IAsyncCache.cs)
or [`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs)
because they are usually tied to the business. Only a single implementation of
[`ICache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/LocalCaching/ICache.cs)
is built-in: 
[`MemoryCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/LocalCaching/MemoryCache.cs).

This library is inspired by a component of the [Criteo](https://medium.com/criteo-engineering)'s
SDK that handles 10B+ requests per second (hint: it's a lot). ModernCaching is production ready
but lacks a way to invalidate data ([#1](https://github.com/verdie-g/modern-caching/issues/1)).

## Installation

ModernCaching is available on [Nuget](https://www.nuget.org/packages/ModernCaching).

```
dotnet add package ModernCaching
```

## Features

- **Strict API**. [`IReadOnlyCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/IReadOnlyCache.cs)
  has only two methods:
  - `TryPeek`, a synchronous operation to only get the value if it's present in
    the local cache and refresh in the background if needed.
  - `TryGetAsync`, an asynchronous operation to get the first fresh value in the
    local cache, distributed cache or the data source, in that order.
- **Performance**. Unlike other caching libraries that use a `string` as a key or an
  `object` as value or both, `ModernCaching` uses a generic key and value. That
  way, getting a value from the local cache doesn't require any allocation for
  simple type keys such as `int` or more complex user-defined objects. See the
  [benchmarks](https://github.com/verdie-g/modern-caching#benchmarks).
- **Resilience**. With its fixed number of layers, each behavior is clearly
  defined when one of these layers is down. For instance, the data source is
  skipped if the distributed cache is unavailable to avoid DDOSing it.
- **Instrumentation**. Metrics are exposed using [OpenTelemetry](https://opentelemetry.io) API.
  Errors from user-code are logged if a logger is specified.

## Example

This example caches the user information. The first layer is implemented with an
in-memory cache, the second one is a redis where we specify how to create the
key and how to serialize the value using the interface `IKeyValueSerializer`.
Behind these two layers stands the `IDataSource`.

```csharp
var cache = await new ReadOnlyCacheBuilder<Guid, User>(new ReadOnlyCacheOptions("user_cache", TimeSpan.FromMinutes(15))
    .WithLocalCache(new MemoryCache<Guid, User>())
    .WithDistributedCache(new RedisAsyncCache(redis), new ProtobufKeyValueSerializer<Guid, User>())
    .WithDataSource(new UserDataSource("Host=localhost;User ID=postgres"))
    .BuildAsync();

Guid userId = new("cb22ff11-4683-4ec3-b212-7f1d0ab378cc");
bool found = cache.TryPeek(userId, out User? user); // Only check local cache with background refresh.
(bool found, User? user) = await cache.TryGetAsync(userId); // Check all layers for a fresh value.
```

The rest of the code as well as other examples can be found in
[src/ModernCaching.ITest](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching.ITest).

## Benchmarks

Benchmark of the very hot path of different caching libraries
([Microsoft.Extensions.Caching.Memory](https://learn.microsoft.com/en-us/dotnet/core/extensions/caching),
[CacheTower](https://github.com/TurnerSoftware/CacheTower),
[Foundatio](https://github.com/FoundatioFx/Foundatio),
[LazyCache](https://github.com/alastairtree/LazyCache),
[FusionCache](https://github.com/jodydonetti/ZiggyCreatures.FusionCache),
[EasyCaching](https://github.com/dotnetcore/EasyCaching),
[CacheManager](https://github.com/MichaCo/CacheManager)),
that is, getting locally cached data.

| Method        | Mean      | Error    | StdDev   | Ratio | RatioSD | Allocated |
|-------------- |----------:|---------:|---------:|------:|--------:|----------:|
| ModernCaching |  13.58 ns | 0.018 ns | 0.015 ns |  1.00 |    0.00 |         - |
| Microsoft     |  32.25 ns | 0.226 ns | 0.189 ns |  2.38 |    0.01 |      32 B |
| CacheTower    |  64.98 ns | 0.368 ns | 0.345 ns |  4.79 |    0.03 |      96 B |
| Foundatio     | 104.12 ns | 0.651 ns | 0.577 ns |  7.67 |    0.04 |     216 B |
| LazyCache     |  77.99 ns | 0.636 ns | 0.594 ns |  5.74 |    0.04 |      96 B |
| FusionCache   |  92.17 ns | 0.405 ns | 0.379 ns |  6.80 |    0.02 |     160 B |
| EasyCaching   | 141.51 ns | 0.455 ns | 0.425 ns | 10.42 |    0.03 |     264 B |
| CacheManager  | 247.69 ns | 2.767 ns | 2.453 ns | 18.27 |    0.16 |     344 B |

This library has similar performance as a raw ConcurrentDictionary since its hot
path is a thin layer around it. It doesn't allocate anything, putting no pressure
on the garbage collector.

Code can be found in [src/ModernCaching.Benchmarks](https://github.com/verdie-g/modern-caching/tree/main/src/ModernCaching.Benchmarks).

## Telemetry

### Metrics / Traces

Metrics and traces are exposed using .NET implementation of the [OpenTelemetry API](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md)
([System.Diagnostics](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics))
under the source name `ModernCaching`. They can be exported using the
[OpenTelemetry .NET SDK](https://github.com/open-telemetry/opentelemetry-dotnet).

### Logs

Use `WithLoggerFactory` on the builder to log all user-code errors coming from
[`IAsyncCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IAsyncCache.cs),
[`IKeyValueSerializer`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DistributedCaching/IKeyValueSerializer.cs) or
[`IDataSource`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/DataSource/IDataSource.cs).

## License

All code found in this repository is licensed under MIT. See the
[LICENSE](https://github.com/verdie-g/crpg/blob/master/LICENSE)
file in the project root for the full license text.
