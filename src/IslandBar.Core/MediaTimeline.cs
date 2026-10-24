namespace IslandBar.Core;

public static class MediaTimeline
{
    /// <summary>
    /// Windows reports the position at a past moment. If the track is playing,
    /// we add the time since then (times the speed) to estimate where it is now.
    /// </summary>
    public static TimeSpan EstimatePosition(TimeSpan reportedPosition, DateTimeOffset reportedAt,
        DateTimeOffset now, bool isPlaying, double playbackRate, TimeSpan duration)
    {
        var position = reportedPosition;
        if (isPlaying)
        {
            var rate = playbackRate > 0 ? playbackRate : 1.0;
            var elapsed = now - reportedAt;
            if (elapsed > TimeSpan.Zero) position += TimeSpan.FromTicks((long)(elapsed.Ticks * rate));
        }
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (duration > TimeSpan.Zero && position > duration) position = duration;
        return position;
    }

    /// <summary>0..1 progress for the bar. Returns 0 when the length is unknown.</summary>
    public static double Progress(TimeSpan position, TimeSpan duration) =>
        duration <= TimeSpan.Zero ? 0 : Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1);
}
