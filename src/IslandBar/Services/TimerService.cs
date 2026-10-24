using System;
using System.Diagnostics;
using System.Windows.Threading;
using IslandBar.Core;

namespace IslandBar.Services;

/// <summary>
/// Wraps the countdown with a UI timer that only runs while the countdown runs,
/// so the app uses no timer time when idle.
/// </summary>
public sealed class TimerService : IDisposable
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private int _lastShownSecond = -1;

    public CountdownTimer Countdown { get; }

    /// <summary>Raised when the shown second changes, or the state changes.</summary>
    public event Action? Updated;
    public event Action? Finished;

    public TimerService()
    {
        Countdown = new CountdownTimer(() => _clock.Elapsed);
        Countdown.Finished += () => { _ticker.Stop(); Finished?.Invoke(); Updated?.Invoke(); };
        _ticker.Tick += (_, _) =>
        {
            Countdown.Tick();
            var second = (int)Math.Ceiling(Countdown.Remaining.TotalSeconds);
            if (second == _lastShownSecond) return;
            _lastShownSecond = second;
            Updated?.Invoke();
        };
    }

    public void SetDuration(TimeSpan duration) { if (Countdown.SetDuration(duration)) Updated?.Invoke(); }
    public void Start() { Countdown.Start(); _lastShownSecond = -1; _ticker.Start(); Updated?.Invoke(); }
    public void Pause() { Countdown.Pause(); _ticker.Stop(); Updated?.Invoke(); }
    public void Resume() { Countdown.Resume(); _ticker.Start(); Updated?.Invoke(); }
    public void Cancel() { Countdown.Cancel(); _ticker.Stop(); Updated?.Invoke(); }

    public void Dispose() => _ticker.Stop();
}
