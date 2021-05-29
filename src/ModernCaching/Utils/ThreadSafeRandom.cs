using System;
using System.Security.Cryptography;
using System.Threading;

namespace ModernCaching.Utils
{
    internal class ThreadSafeRandom : IRandom
    {
        // https://devblogs.microsoft.com/pfxteam/getting-random-numbers-in-a-thread-safe-way/
        private static readonly RNGCryptoServiceProvider StrongRng = new();

        private readonly ThreadLocal<Random> _instance = new(() =>
        {
            byte[] buffer = new byte[4];
            StrongRng.GetBytes(buffer);
            return new Random(BitConverter.ToInt32(buffer, 0));
        });

        /// <inheritdoc/>
        public int Next(int minValue, int maxValue)
        {
            return _instance.Value.Next(minValue, maxValue);
        }
    }
}
