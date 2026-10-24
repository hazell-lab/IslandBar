using System;
using System.Windows.Threading;
using IslandBar.Core;
using IslandBar.Interop;

namespace IslandBar.Services;

/// <summary>
/// Tells us when a fullscreen app or presentation is in front.
/// It reacts to foreground-window changes, plus a slow 2-second check to catch F11-style toggles.
/// Windows has no event for "an app went fullscreen", so this check is a documented limitation.
/// </summary>
public sealed class FullscreenWatcher : IDisposable
{
    private readonly DispatcherTimer _slowCheck = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly NativeMethods.WinEventProc _callback;   // kept in a field so it is not garbage collected
    private IntPtr _hook;
    private bool _isFullscreen;

    public bool IsFullscreen => _isFullscreen;
    public event Action<bool>? FullscreenChanged;

    public FullscreenWatcher()
    {
        _callback = (_, _, _, _, _, _, _) => Check();
        _slowCheck.Tick += (_, _) => Check();
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = NativeMethods.SetWinEventHook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
        _slowCheck.Start();
        Check();
    }

    public void Stop()
    {
        _slowCheck.Stop();
        if (_hook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_hook); _hook = IntPtr.Zero; }
        if (_isFullscreen) { _isFullscreen = false; FullscreenChanged?.Invoke(false); }
    }

    private void Check()
    {
        var now = NativeMethods.SHQueryUserNotificationState(out var state) == 0 && FullscreenLogic.IsFullscreen(state);
        if (now == _isFullscreen) return;
        _isFullscreen = now;
        FullscreenChanged?.Invoke(now);
    }

    public void Dispose() => Stop();
}
