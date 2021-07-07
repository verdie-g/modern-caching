using System;
using System.IO;
using Microsoft.Extensions.ObjectPool;

namespace ModernCaching.Utils
{
    internal static class UtilsCache
    {
        public static readonly ITimer LoadingTimer = new TimerWrapper(TimeSpan.FromSeconds(3));
        public static readonly IDateTime DateTime = new MachineDateTime();
        public static readonly IRandom Random = new ThreadSafeRandom();
        public static readonly DefaultObjectPool<MemoryStream> MemoryStreamPool = new(new PooledMemoryStreamPolicy());
    }
}
