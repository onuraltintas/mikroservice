namespace SpeedReading.Application.ExerciseSessions;

public sealed record VisualExpansionRoundResult(
    bool IsAccepted,
    bool IsCorrect,
    int ResponseTimeMs);

public static class VisualExpansionRoundRules
{
    public static VisualExpansionRoundResult Evaluate(
        IReadOnlyList<string> expectedAnswers,
        IReadOnlyList<string> submittedAnswers,
        int responseTimeMs,
        int minimumResponseTimeMs,
        int maximumResponseTimeMs)
    {
        var timingIsPlausible = responseTimeMs >= Math.Max(0, minimumResponseTimeMs)
            && responseTimeMs <= Math.Max(minimumResponseTimeMs, maximumResponseTimeMs);
        if (!timingIsPlausible)
            return new VisualExpansionRoundResult(false, false, responseTimeMs);

        var isCorrect = expectedAnswers.Count > 0
            && submittedAnswers.Count == expectedAnswers.Count
            && expectedAnswers.Select(Normalize).SequenceEqual(submittedAnswers.Select(Normalize));

        return new VisualExpansionRoundResult(true, isCorrect, responseTimeMs);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
