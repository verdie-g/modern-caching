namespace ModernCaching.DistributedCaching;

/// <summary>
/// Key serializer for an <see cref="IAsyncCache"/>.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the <see cref="IAsyncCache"/>.</typeparam>
public interface IKeySerializer<TKey> where TKey : notnull
{
    /// <summary>
    /// Converts <paramref name="key"/> to its equivalent string representation.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The string representation of <paramref name="key" />.</returns>
    string SerializeKey(TKey key);

    /// <summary>
    /// Converts <see cref="keyStr"/> to its equivalent object representation.
    /// </summary>
    /// <param name="keyStr">The string representation of a key.</param>
    /// <returns>The key object.</returns>
    TKey DeserializeKey(string keyStr);
}
