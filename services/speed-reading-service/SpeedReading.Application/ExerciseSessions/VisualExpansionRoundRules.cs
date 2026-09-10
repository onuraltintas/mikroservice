namespace SpeedReading.Application.ExerciseSessions;

public sealed record VisualExpansionRoundResult(
    bool IsAccepted,
    bool IsCorrect,
    int ResponseTimeMs);

public static class VisualExpansionRoundRules
{
    private static readonly string[] Letters = "ABCDEFGHKLMNPRSTUVYZ"
        .Select(character => character.ToString())
        .ToArray();
    private static readonly string[] Numbers = Enumerable.Range(1, 9)
        .Select(number => number.ToString())
        .ToArray();
    private static readonly string[] Symbols = ["★", "●", "▲", "■", "◆", "+", "×", "÷"];
    private static readonly string[] Words = ["EV", "SU", "GÜN", "YOL", "KUŞ", "AY", "EL", "DAĞ"];

    public static IReadOnlyList<string> CreateStimuli(
        int sessionSeed,
        int roundIndex,
        string stimulusType,
        int count)
    {
        var pool = stimulusType.Trim().ToLowerInvariant() switch
        {
            "letter" => Letters,
            "number" => Numbers,
            "symbol" => Symbols,
            "word" => Words,
            _ => throw new ArgumentOutOfRangeException(nameof(stimulusType), stimulusType, "Unsupported stimulus type.")
        };
        if (count <= 0 || count > pool.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        var random = new Random(unchecked(sessionSeed * 397 ^ roundIndex));
        return pool.OrderBy(_ => random.Next()).Take(count).ToArray();
    }

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
