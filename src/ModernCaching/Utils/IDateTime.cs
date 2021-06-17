using System;

namespace ModernCaching.Utils
{
    internal interface IDateTime
    {
        /// <summary>
        /// Gets a <see cref="DateTime"/> object that is set to the current date and time on this computer, expressed
        /// as the Coordinated Universal Time (UTC).
        /// </summary>
        DateTime UtcNow { get; }
    }
}
