using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Sessions;
using OwnedExerciseSessionStatus = SpeedReading.Domain.Sessions.ExerciseSessionStatus;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Decision 12.11.2026: only active time is recorded as study time (see <see cref="SpeedReadingActiveTimeRules"/>).
/// Every exercise step, pause, resume and completion is an activity point. The idle part of a long stretch between
/// two points is kept in the session state, next to the pause counters, and is subtracted where the recorded time
/// and the reading speed are calculated. The pause counters themselves, the time limit and every timing validation
/// are left as they were.
/// </summary>
internal sealed partial class OwnedSpeedReadingExerciseSessions
{
    /// <summary>
    /// Call once per saved request, before the request changes the session's status. A repeated request is safe:
    /// the stretch is measured from the activity point saved with the session, and a request that loses the
    /// concurrency check is reloaded and measured again from the saved state.
    /// </summary>
    private static void ObserveActivity(ExerciseSession session, SessionState state, DateTime now)
    {
        var utcNow = now.ToUniversalTime();
        var lastActivity = state.LastActivityAt ?? session.UpdatedAt ?? session.StartTime;
        // While the session is paused the whole stretch is already excluded as paused time.
        if (session.Status == OwnedExerciseSessionStatus.Active)
        {
            var idle = SpeedReadingActiveTimeRules.CalculateIdleExcessMilliseconds(lastActivity, utcNow, ResolveGapCapSeconds(state));
            if (idle > 0)
            {
                // Reset when the session clock starts (EnsureTimingStarted), so it only holds idle time since then.
                state.IdleExcessMilliseconds += idle;
                if (IsReadingPhaseOpen(state))
                    state.ReadingIdleExcessMilliseconds += idle;
                if (IsFocusClockRunning(state))
                    state.FocusIdleExcessMilliseconds += idle;
            }
        }

        if (!state.LastActivityAt.HasValue || state.LastActivityAt.Value < utcNow)
            state.LastActivityAt = utcNow;
    }

    /// <summary>The focus clock runs from focus_start until the exercise is finished (see StopFocusClock).</summary>
    private static bool IsFocusClockRunning(SessionState state) =>
        state.FocusStartTime.HasValue && !state.FocusEndTime.HasValue;

    /// <summary>
    /// The recorded focus time ends when the exercise is finished, not when its result is saved: the result may be
    /// saved much later (a lost connection, a tab left on the last screen).
    /// </summary>
    private static void StopFocusClock(SessionState state, DateTime now)
    {
        if (state.FocusCompleted && IsFocusClockRunning(state))
            state.FocusEndTime = now.ToUniversalTime();
    }

    private static bool IsReadingPhaseOpen(SessionState state) =>
        state.ReadingStartTime.HasValue && !state.ReadingEndTime.HasValue;

    private static int ResolveGapCapSeconds(SessionState state) =>
        SpeedReadingActiveTimeRules.ResolveGapCapSeconds(
            IsReadingPhaseOpen(state) || state.AdaptiveStageStartedAt.HasValue,
            Math.Max(state.WordCount, state.AdaptiveStageStartedAt.HasValue ? state.AdaptiveTransferWordCount : 0),
            state.ReadingMinimumMs,
            state.ReadingMaximumMs,
            state.TrackingDurationSeconds,
            IsFocusExercise(state) && IsFocusClockRunning(state)
                ? (int)Math.Ceiling(Math.Max(0L, (long)state.TotalSteps * state.FocusSpeedMs) / 1000d)
                : 0);

    private static int IdleSeconds(long idleMilliseconds) =>
        (int)Math.Round(Math.Max(0, idleMilliseconds) / 1000d);
}
