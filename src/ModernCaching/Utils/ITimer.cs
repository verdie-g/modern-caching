using System;
using System.Timers;

namespace ModernCaching.Utils;

internal interface ITimer : IDisposable
{
    event ElapsedEventHandler Elapsed;
}
