# modern-caching

A 2-layer, performant and predicable caching solution for modern .NET.

## Features

- Strict API. [`IReadOnlyCache`](https://github.com/verdie-g/modern-caching/blob/main/src/ModernCaching/IReadOnlyCache.cs)
  has only two methods:
  - `TryGet`, a synchronous operation to only get the value if it's present in
    the local cache.
  - `TryGetAsync`, an asynchronous operation to get the first fresh value in the
    local cache, distributed cache or the data source, in that order.
- Performance. Unlike other caching libraries that use a `string` as a key or an
  `object` as value or both, `ModernCaching` uses a generic key and value. That
  way, getting a value from the local cache doesn't require any allocation for
  simple type keys such as `int` or more complex user-defined objects.
- Predictability. Since the number of layers is fixed, it's easy to define
  what should be done when one of these layers is down.

## Example

This example caches a mapping of "external id" to "internal id". The first
layer is implemented with a concurrent dictionary, the second one is a memcache
where we specify how to create the key and how to serialize the value using the
interface `IKeyValueSerializer`. Behind these two layers stands the `IDataSource`
which is usually a relational database but it's up to the implementation.

```csharp
var cache = await new ReadOnlyCacheBuilder<int, int?>("external_to_internal_id_cache", new ExternalToInternalIdDataSource())
    .WithLocalCache(new ConcurrentDictionaryCache<int, int?>())
    .WithDistributedCache(new MemcacheAsyncCache(), new ExternalToInternalIdKeyValueSerializer())
    .WithPreload(_ => Task.FromResult<IEnumerable<int>>(new[] { 1, 2, 3 }), null)
    .BuildAsync();

bool found = cache.TryGet(5, out int? internalId); // Only check local cache.
(bool found, int value) res = await cache.TryGetAsync(5); // Check all layers.

class ExternalToInternalIdDataSource : IDataSource<int, int?>
{
    public async IAsyncEnumerable<DataSourceResult<int, int?>> LoadAsync(IEnumerable<int> keys, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using SqlConnection connection = new("Data Source=(local)");
        string keysStr = string.Join(',', keys);
        SqlCommand command = new($"SELECT external_id, internal_id FROM users WHERE external_id IN [{keysStr}]", connection);
        await connection.OpenAsync(cancellationToken);
        SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            int externalId = reader.GetInt32(0);
            int internalId = reader.GetInt32(1);
            yield return new DataSourceResult<int, int?>(externalId, internalId, TimeSpan.FromHours(1));
        }
        await reader.CloseAsync();
    
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
        return valueBytes.Length == 0 ? null : BitConverter.ToInt32(valueBytes);
    }
}
```
