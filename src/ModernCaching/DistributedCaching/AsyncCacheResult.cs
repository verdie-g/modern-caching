namespace ModernCaching.DistributedCaching
{
    /// <summary>
    /// Result of the loading of a key from an <see cref="IAsyncCache"/>.
    /// </summary>
    /// <param name="Status">The status of the result.</param>
    /// <param name="Value">The value associated with the key. Null if status is <see cref="AsyncCacheStatus.Miss"/> or
    /// <see cref="AsyncCacheStatus.Error"/>.</param>
    public record AsyncCacheResult(AsyncCacheStatus Status, byte[]? Value);
}
