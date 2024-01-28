using System;
using System.Security.Cryptography;
using System.Threading;

namespace ModernCaching.Utils;

internal sealed class ThreadSafeRandom : IRandom
{
    private readonly ThreadLocal<Random> _instance = new(static () =>
    {
        byte[] buffer = new byte[4];
        RandomNumberGenerator.Fill(buffer);
        return new Random(BitConverter.ToInt32(buffer, 0));
    });

    /// <inheritdoc/>
    public int Next(int minValue, int maxValue)
    {
        return _instance.Value!.Next(minValue, maxValue);
    }
}
