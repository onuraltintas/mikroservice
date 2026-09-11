namespace SpeedReading.Application.ExerciseSessions;

public static class FocusTrialTimingRules
{
    public static int ExpectedIndex(
        DateTime startedAt,
        DateTime now,
        int pausedSeconds,
        int speedMilliseconds,
        int totalSteps)
    {
        if (speedMilliseconds < 1)
            throw new ArgumentOutOfRangeException(nameof(speedMilliseconds));
        if (totalSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(totalSteps));
        var activeMilliseconds = Math.Max(
            0,
            (now.ToUniversalTime() - startedAt.ToUniversalTime()).TotalMilliseconds
                - Math.Max(0, pausedSeconds) * 1000d);
        return Math.Clamp(
            (int)Math.Floor(activeMilliseconds / speedMilliseconds),
            0,
            totalSteps - 1);
    }

    public static bool CanPresent(int requestedIndex, int presentedIndex, int expectedIndex)
    {
        if (requestedIndex != presentedIndex + 1 || requestedIndex > expectedIndex)
            return false;
        return presentedIndex < 0 || requestedIndex >= Math.Max(0, expectedIndex - 1);
    }

    public static bool IsCurrentTrial(int requestedIndex, int presentedIndex) =>
        requestedIndex == presentedIndex;

    public static bool IsAssessmentResponseOnTime(
        DateTime presentedAt,
        DateTime now,
        int pausedSecondsSincePresentation,
        int speedMilliseconds)
    {
        if (speedMilliseconds < 1)
            throw new ArgumentOutOfRangeException(nameof(speedMilliseconds));
        var elapsedMilliseconds = Math.Max(
            0,
            (now.ToUniversalTime() - presentedAt.ToUniversalTime()).TotalMilliseconds
                - Math.Max(0, pausedSecondsSincePresentation) * 1000d);
        return elapsedMilliseconds <= speedMilliseconds * 2d;
    }
}
