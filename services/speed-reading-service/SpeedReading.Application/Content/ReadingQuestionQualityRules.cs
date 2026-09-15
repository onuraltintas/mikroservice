namespace SpeedReading.Application.Content;

public static class ReadingQuestionQualityRules
{
    public static bool HasScorableAnswerKey(string? answerKey) =>
        answerKey?.Trim().ToUpperInvariant() is "A" or "B" or "C" or "D";
}
