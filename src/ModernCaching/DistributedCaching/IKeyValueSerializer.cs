using System.IO;

namespace ModernCaching.DistributedCaching;

/// <summary>
/// Key and value serializer for an <see cref="IAsyncCache"/>.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the <see cref="IAsyncCache"/>.</typeparam>
/// <typeparam name="TValue">The type of the values in the <see cref="IAsyncCache"/>.</typeparam>
public interface IKeyValueSerializer<in TKey, TValue> where TKey : notnull
{
    /// <summary>
    /// Version of the <typeparamref name="TValue"/> schema. Bump the <see cref="Version"/> everytime a breaking change
    /// to the serialization/deserialization of <typeparamref name="TValue"/> is made. The <see cref="Version"/>
    /// is included in the <see cref="IAsyncCache"/>'s key so that different versions are stored in different keys.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Converts <paramref name="key"/> to its equivalent string representation.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The string representation of <paramref name="key" />.</returns>
    string SerializeKey(TKey key);

    /// <summary>
    /// Writes the <paramref name="value"/> to <paramref name="stream"/>.
    /// </summary>
    /// <remarks>The method should handle null values if the data source can return null values.</remarks>
    /// <param name="value">The value to write. Can be null if the data source returned null.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <remarks>Serializing the <paramref name="value"/> as 0 bytes is reserved to represent cache entry with no value.</remarks>
    void SerializeValue(TValue value, Stream stream);

    /// <summary>
    /// Reads a value from its bytes representation.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>A <typeparamref name="TValue"/>. Null can be returned if what is read represents null.</returns>
    TValue DeserializeValue(Stream stream);
}
