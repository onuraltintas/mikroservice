namespace SpeedReading.Application.ExerciseSessions;

/// <summary>
/// Decision 12.11.2026: only active time is recorded as study time. The server sees an open session only when the
/// student does something (an exercise step, a pause, a resume, the completion). A stretch between two of those
/// points that is longer than the cap counts as the cap; the rest is treated as if the session had been paused.
/// The time limit of a timed session is not affected: it still counts the whole stretch.
/// </summary>
public static class SpeedReadingActiveTimeRules
{
    /// <summary>The longest stretch without server activity that still counts in full.</summary>
    public const int MaxActiveGapSeconds = 300;

    /// <summary>
    /// A text is read in one step, without any server activity in between. The cap for that step leaves room for a
    /// slow reader: twice as slow as the lowest speed the service accepts as a valid measurement (20 WPM).
    /// </summary>
    public const int SlowReaderWordsPerMinute = 40;

    /// <summary>
    /// The cap for one stretch. It is never shorter than a single step may legitimately take: while a text is open,
    /// the configured reading limit, a paced display's own running time and the time a slow reader needs for the
    /// text; for an eye-tracking run, its configured duration; for a running focus stream, the time the stream
    /// itself takes (it can be watched to the end without a single tap).
    /// </summary>
    public static int ResolveGapCapSeconds(
        bool readingPhaseOpen,
        int wordCount,
        int readingMinimumMs,
        int readingMaximumMs,
        int trackingDurationSeconds,
        int focusStreamSeconds = 0)
    {
        var cap = Math.Max(MaxActiveGapSeconds, Math.Max(trackingDurationSeconds, focusStreamSeconds));
        if (!readingPhaseOpen)
            return cap;

        cap = Math.Max(cap, (int)Math.Ceiling(Math.Max(readingMinimumMs, readingMaximumMs) / 1000d));
        return Math.Max(cap, (int)Math.Ceiling(Math.Max(0, wordCount) * 60d / SlowReaderWordsPerMinute));
    }

    /// <summary>The part of a stretch that does not count as study time.</summary>
    public static long CalculateIdleExcessMilliseconds(DateTime lastActivity, DateTime now, int capSeconds)
    {
        var gap = (now.ToUniversalTime() - lastActivity.ToUniversalTime()).TotalMilliseconds;
        return (long)Math.Max(0, Math.Round(gap - Math.Max(0, capSeconds) * 1000d));
    }

    /// <summary>
    /// When a session that was left open ends. A paused session did no work after its pause; an active one is given
    /// one cap after its last activity, and never a time in the future.
    /// </summary>
    public static DateTime ResolveAbandonTime(DateTime lastActivity, DateTime? pausedAt, DateTime now, int capSeconds)
    {
        if (pausedAt.HasValue)
            return pausedAt.Value.ToUniversalTime();

        var end = lastActivity.ToUniversalTime().AddSeconds(Math.Max(0, capSeconds));
        return end < now.ToUniversalTime() ? end : now.ToUniversalTime();
    }
}
