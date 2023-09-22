using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace ModernCaching.Utils;

internal class HighReadLowWriteCounter
{
    private readonly PaddedLong[] _counts = new PaddedLong[Environment.ProcessorCount];

    public long Value => _counts.Sum(c => c.Value);

    public void Increment() =>
        Interlocked.Increment(ref _counts[CurrentProcessorIndex].Value);

    public void Decrement() =>
        Interlocked.Decrement(ref _counts[CurrentProcessorIndex].Value);

    public void Add(long value) =>
        Interlocked.Add(ref _counts[CurrentProcessorIndex].Value, value);

    private int CurrentProcessorIndex =>
        Thread.GetCurrentProcessorId() % _counts.Length;

    // Pad the long to be the size of a cache line (64 bytes) to prevent false sharing.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct PaddedLong
    {
        [FieldOffset(0)]
        public long Value;
    }
}