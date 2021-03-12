namespace ModernCaching.DistributedCaching
{
    public readonly struct AsyncCacheResult
    {
        public readonly AsyncCacheStatus Status;
        public readonly byte[] Value;

        public AsyncCacheResult(AsyncCacheStatus status, byte[] value)
        {
            Status = status;
            Value = value;
        }
    }
}
