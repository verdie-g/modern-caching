using System;
using ModernCaching.DataSource;

namespace ModernCaching;

/// <summary>
/// Provides options to be used with <see cref="ReadOnlyCacheBuilder{TKey,TValue}"/>.
/// </summary>
public sealed record ReadOnlyCacheOptions
{
    /// <summary>
    /// Whether keys that were not found in the <see cref="IDataSource{TKey,TValue}"/> should be cached. If true,
    /// the time-to-live of the entries are <see cref="DefaultTimeToLive"/>. Use this option to avoid load on the
    /// data source when many keys don't have a value associated in the data source. WARNING: if the key is a user
    /// input or that the cardinality of the key is very high it can lead to <see cref="OutOfMemoryException"/>.
    /// For that reason, the default value is false.
    /// </summary>
    public bool CacheDataSourceMisses { get; init; }

    /// <summary>
    /// The time-to-live for <see cref="CacheDataSourceMisses"/>. The default value is 15 minutes.
    /// </summary>
    public TimeSpan DefaultTimeToLive { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Timespan to wait before <see cref="IDataSource{TKey,TValue}.LoadAsync"/> times out. The default value
    /// is 15 seconds.
    /// </summary>
    public TimeSpan LoadTimeout { get; init; } = TimeSpan.FromSeconds(15);
}
