using System;
using System.Threading;

namespace IslandBar.Services;

/// <summary>
/// Makes sure only one copy runs. A second launch asks the first copy to open Settings, then exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private readonly RegisteredWaitHandle? _wait;

    public bool IsFirst { get; }

    public SingleInstance(Action onSecondLaunch)
    {
        _mutex = new Mutex(true, @"Local\IslandBar.Mutex", out var created);
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\IslandBar.Signal");
        IsFirst = created;
        if (IsFirst)
            _wait = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => onSecondLaunch(), null, Timeout.Infinite, false);
        else
            _signal.Set();
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        _signal.Dispose();
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
