namespace ModernCaching.DistributedCaching
{
    public enum AsyncCacheStatus
    {
        /// <summary>
        /// The key was found.
        /// </summary>
        Hit,

        /// <summary>
        /// The key was not found.
        /// </summary>
        Miss,

        /// <summary>
        /// An error occured.
        /// </summary>
        Error,
    }
}
