using System;
using System.IO;

namespace ModernCaching.DistributedCaching
{
    /// <summary>
    /// Key stringifier and value serializer for an <see cref="IAsyncCache"/>.
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
        string StringifyKey(TKey key);

        /// <summary>
        /// Writes the <paramref name="value"/> to <paramref name="writer"/>.
        /// </summary>
        /// <remarks>The method should handle null values if the data source can return null values.</remarks>
        /// <param name="value">The value to write. Can be null if the data source returned null.</param>
        /// <param name="writer">The writer to write to.</param>
        void SerializeValue(TValue value, BinaryWriter writer);

        /// <summary>
        /// Reads a value from its bytes representation.
        /// </summary>
        /// <param name="valueBytes">The bytes of the value.</param>
        /// <returns>A <typeparamref name="TValue"/>. Null can be returned if <see cref="valueBytes"/> represents null.</returns>
        TValue DeserializeValue(ReadOnlySpan<byte> valueBytes);
    }
}
