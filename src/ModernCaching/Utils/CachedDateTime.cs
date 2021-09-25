using System;

namespace ModernCaching.Utils
{
    /// <summary>
    /// An implementation of <see cref="IDateTime"/> that reuses the loading timer to compute UtcNow. It was measured
    /// that <see cref="DateTime.UtcNow"/> would take 75% of the time of a TryPeek hence this class.
    /// </summary>
    internal sealed class CachedDateTime : IDateTime
    {
        public CachedDateTime(ITimer timer)
        {
            UtcNow = DateTime.UtcNow;
            timer.Elapsed += (_, __) => UtcNow = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public DateTime UtcNow { get; private set; }
    }
}
