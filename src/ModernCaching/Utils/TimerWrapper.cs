using System;
using System.Timers;

namespace ModernCaching.Utils;

internal sealed class TimerWrapper : ITimer
{
    private readonly Timer _underlyingTimer;

    public TimerWrapper(TimeSpan interval)
    {
        _underlyingTimer = new Timer(interval.TotalMilliseconds) { Enabled = true };
    }

    public event ElapsedEventHandler Elapsed
    {
        add => _underlyingTimer.Elapsed += value;
        remove => _underlyingTimer.Elapsed -= value;
    }

    public void Dispose() => _underlyingTimer.Dispose();
}
