using System;
using System.Text.RegularExpressions;
using ModernCaching.DataSource;

namespace ModernCaching;

/// <summary>
/// Provides options to be used with <see cref="ReadOnlyCacheBuilder{TKey,TValue}"/>.
/// </summary>
public sealed record ReadOnlyCacheOptions
{
    /// <param name="name">Name of cache. Used in the distributed cache key, logging and metrics.</param>
    /// <param name="timeToLive">Time after which keys are considered stale in the cache.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeToLive"/> is zero or negative.</exception>
    public ReadOnlyCacheOptions(string name, TimeSpan timeToLive)
    {
        Name = NormalizeCacheName(name ?? throw new ArgumentNullException(nameof(name)));
        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive));
        }
        TimeToLive = timeToLive;
    }

    /// <summary>
    /// Name of cache. Used in the distributed cache key, logging and metrics.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Time after which keys are considered stale in the cache.
    /// </summary>
    public TimeSpan TimeToLive { get; set; }

    /// <summary>
    /// Whether keys that were not found in the <see cref="IDataSource{TKey,TValue}"/> should be cached. Use this option
    /// to avoid load on the data source when many keys don't have a value associated in the data source. WARNING: if
    /// the key is a user input or that the cardinality of the key is very high it can lead to <see cref="OutOfMemoryException"/>.
    /// For that reason, the default value is false.
    /// </summary>
    public bool CacheDataSourceMisses { get; init; }

    /// <summary>
    /// Timespan to wait before <see cref="IDataSource{TKey,TValue}.LoadAsync"/> times out. The default value
    /// is 15 seconds.
    /// </summary>
    public TimeSpan LoadTimeout { get; init; } = TimeSpan.FromSeconds(15);

    private string NormalizeCacheName(string name)
    {
        return Regex.Replace(name, "[^a-zA-Z0-9.\\-_]", "");
    }
}
