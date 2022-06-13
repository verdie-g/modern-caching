using System.IO;
using Microsoft.Extensions.ObjectPool;

namespace ModernCaching.Utils;

internal sealed class PooledMemoryStreamPolicy : IPooledObjectPolicy<MemoryStream>
{
    private const int InitialCapacity = 128;
    private const int MaximumRetainedCapacity = 4096;

    public MemoryStream Create()
    {
        return new(InitialCapacity);
    }

    public bool Return(MemoryStream stream)
    {
        if (stream.Length > MaximumRetainedCapacity)
        {
            // Too big. Discard it.
            return false;
        }

        stream.SetLength(0);
        return true;
    }
}
