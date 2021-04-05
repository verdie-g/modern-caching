namespace ModernCaching.DistributedCaching
{
    public record AsyncCacheResult(AsyncCacheStatus Status, byte[] Value);
}
