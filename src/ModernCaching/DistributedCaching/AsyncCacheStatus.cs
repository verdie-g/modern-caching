namespace ModernCaching.DistributedCaching;

/// <summary>
/// Status of an <see cref="IAsyncCache"/> operation.
/// </summary>
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
