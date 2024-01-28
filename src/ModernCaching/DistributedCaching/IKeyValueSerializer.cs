namespace ModernCaching.DistributedCaching;

/// <summary>
/// Key and value serializer for an <see cref="IAsyncCache"/>.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the <see cref="IAsyncCache"/>.</typeparam>
/// <typeparam name="TValue">The type of the values in the <see cref="IAsyncCache"/>.</typeparam>
public interface IKeyValueSerializer<TKey, TValue> : IKeySerializer<TKey>, IValueSerializer<TValue> where TKey : notnull
{
}
