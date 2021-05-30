namespace ModernCaching.DistributedCaching
{
    /// <summary>
    /// Result of the loading of a key from an <see cref="IAsyncCache"/>.
    /// </summary>
    public readonly struct AsyncCacheResult
    {
        /// <summary>
        /// The status of the result.
        /// </summary>
        public readonly AsyncCacheStatus Status;

        /// <summary>
        /// The value associated with the key. Null if status is <see cref="AsyncCacheStatus.Miss"/> or <see cref="AsyncCacheStatus.Error"/>.
        /// </summary>
        public readonly byte[]? Value;

        public AsyncCacheResult(AsyncCacheStatus status, byte[]? value)
        {
            Status = status;
            Value = value;
        }

        public void Deconstruct(out AsyncCacheStatus status, out byte[]? value)
        {
            status = Status;
            value = Value;
        }
    }
}
