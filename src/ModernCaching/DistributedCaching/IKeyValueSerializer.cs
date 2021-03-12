using System;
using System.IO;

namespace ModernCaching.DistributedCaching
{
    public interface IKeyValueSerializer<in TKey, TValue>
    {
        int Version { get; }
        string StringifyKey(TKey key);

        // Should handle null if your cache supports null value
        void SerializeValue(TValue value, Stream stream);
        TValue DeserializeValue(ReadOnlySpan<byte> valueBytes);
    }
}
