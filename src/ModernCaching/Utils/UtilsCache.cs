using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.ObjectPool;
using ModernCaching.Telemetry;

namespace ModernCaching.Utils;

internal static class UtilsCache
{
    private static readonly AssemblyName AssemblyName = typeof(CacheMetrics).Assembly.GetName();
    private static readonly string InstrumentationName = AssemblyName.Name!;
    private static readonly string InstrumentationVersion = AssemblyName.Version!.ToString();

    public static readonly ITimer LoadingTimer = new TimerWrapper(TimeSpan.FromSeconds(3));
    public static readonly IDateTime DateTime = new CachedDateTime(LoadingTimer);
    public static readonly DefaultObjectPool<MemoryStream> MemoryStreamPool = new(new PooledMemoryStreamPolicy());
    public static readonly Meter Meter = new(InstrumentationName, InstrumentationVersion);
    public static readonly ActivitySource ActivitySource = new(InstrumentationName, InstrumentationVersion);
}
