namespace IslandBar.Core;

public enum TimerState { Idle, Running, Paused, Finished }

/// <summary>
/// A countdown that is driven by a clock you give it, so it is easy to test.
/// The UI just calls Tick() now and then and reads Remaining.
/// </summary>
public sealed class CountdownTimer
{
    private readonly Func<TimeSpan> _now;       // a steady clock (like Stopwatch time)
    private TimeSpan _endAt;                    // clock value when it finishes (Running)
    private TimeSpan _frozenRemaining;          // time left (Paused)

    public CountdownTimer(Func<TimeSpan> now) => _now = now;

    public TimerState State { get; private set; } = TimerState.Idle;
    public TimeSpan Duration { get; private set; } = TimeSpan.FromMinutes(5);

    public event Action? Finished;

    public TimeSpan Remaining => State switch
    {
        TimerState.Running => Max0(_endAt - _now()),
        TimerState.Paused => _frozenRemaining,
        TimerState.Finished => TimeSpan.Zero,
        _ => Duration,
    };

    /// <summary>Changes the length. Only allowed when not running.</summary>
    public bool SetDuration(TimeSpan duration)
    {
        if (State is TimerState.Running or TimerState.Paused) return false;
        Duration = Clamp(duration);
        State = TimerState.Idle;
        return true;
    }

    public void Start()
    {
        if (State is TimerState.Running or TimerState.Paused) return;
        _endAt = _now() + Duration;
        State = TimerState.Running;
    }

    public void Pause()
    {
        if (State != TimerState.Running) return;
        _frozenRemaining = Remaining;
        State = TimerState.Paused;
    }

    public void Resume()
    {
        if (State != TimerState.Paused) return;
        _endAt = _now() + _frozenRemaining;
        State = TimerState.Running;
    }

    /// <summary>Stops and goes back to Idle (also used to dismiss a finished timer).</summary>
    public void Cancel() => State = TimerState.Idle;

    /// <summary>Call regularly. Raises Finished once when time runs out.</summary>
    public void Tick()
    {
        if (State == TimerState.Running && _now() >= _endAt)
        {
            State = TimerState.Finished;
            Finished?.Invoke();
        }
    }

    public static string Format(TimeSpan span)
    {
        // Round up so "0:00" only shows when the timer is really done.
        var totalSeconds = (int)Math.Ceiling(Max0(span).TotalSeconds);
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes:00}:{seconds:00}";
    }

    private static TimeSpan Max0(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;
    private static TimeSpan Clamp(TimeSpan t) =>
        t < TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(10) :
        t > TimeSpan.FromHours(3) ? TimeSpan.FromHours(3) : t;
}
