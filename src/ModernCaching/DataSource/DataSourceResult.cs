using System;

namespace ModernCaching.DataSource;

/// <summary>
/// Result of the loading of a key from an <see cref="IDataSource{TKey,TValue}"/>.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <param name="Key">The key.</param>
/// <param name="Value">The value associated with the key. Can be null.</param>
/// <param name="TimeToLive">Duration after which the data is considered stale.</param>
public sealed record DataSourceResult<TKey, TValue>(TKey Key, TValue Value, TimeSpan TimeToLive);
