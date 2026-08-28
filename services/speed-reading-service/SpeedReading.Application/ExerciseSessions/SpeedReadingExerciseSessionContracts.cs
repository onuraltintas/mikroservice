using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeedReading.Application.ExerciseSessions;

public enum ExerciseSessionStatus
{
    Active = 1,
    Paused = 2,
    Completed = 3,
    Timeout = 4,
    Abandoned = 5
}

public enum SpeedReadingMeasurementStatus
{
    NotMeasured = 0,
    Measured = 1
}

public sealed class StartExerciseSessionRequest
{
    public Guid ExerciseId { get; init; }
    public Guid? ReadingTextId { get; init; }
    public Guid? StudentAssignmentId { get; init; }
    public Guid? AssessmentAttemptId { get; init; }
    public Dictionary<string, JsonElement>? CustomData { get; init; }
}

public sealed record StartExerciseSessionResponse(
    Guid SessionId,
    Guid ExerciseId,
    string ExerciseTypeName,
    ExerciseSessionStatus Status,
    DateTime StartTime,
    int TotalSteps,
    JsonElement InitialData,
    JsonElement Configuration);

public sealed class ExerciseActionRequest
{
    public Guid? ActionId { get; init; }
    public string? Action { get; init; }
    public int? Number { get; init; }
    public int? Row { get; init; }
    public int? Col { get; init; }
    public string? Answer { get; init; }
    public List<string>? Answers { get; init; }
    public Guid? QuestionId { get; init; }
    public int? ResponseTime { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public double? EyeX { get; init; }
    public double? EyeY { get; init; }
    public Dictionary<string, JsonElement>? CustomData { get; init; }
}

public sealed record ExerciseActionValidationResponse(
    bool IsValid,
    string Message,
    int? NextNumber,
    int? NextStep,
    string? NextWord,
    bool IsCompleted,
    bool? IsCorrect,
    string? CorrectAnswer,
    string? Explanation,
    [property: JsonPropertyName("currentWPM")] decimal? CurrentWPM,
    JsonElement? FeedbackData);

public sealed class CompleteExerciseSessionRequest
{
    public List<ExerciseQuestionAnswer>? QuestionAnswers { get; init; }
    public Dictionary<string, JsonElement>? CustomData { get; init; }
    public bool IsAssessmentMode { get; init; }
}

public sealed class ExerciseQuestionAnswer
{
    public Guid QuestionId { get; init; }
    public string Answer { get; init; } = string.Empty;
    public bool? IsCorrect { get; init; }
    public int TimeSpentSeconds { get; init; }
    public int BloomLevel { get; init; }
}

public sealed record ExerciseSessionProgress(
    Guid SessionId,
    ExerciseSessionStatus Status,
    int CurrentStep,
    int TotalSteps,
    decimal ProgressPercentage,
    int CorrectCount,
    int IncorrectCount,
    int ElapsedSeconds,
    int? TimeLimitSeconds,
    bool IsTimedOut);

public sealed record ExerciseSessionDetails(
    Guid Id,
    Guid ExerciseId,
    string ExerciseName,
    string ExerciseType,
    ExerciseSessionStatus Status,
    DateTime StartTime,
    DateTime? EndTime,
    int CurrentStep,
    int TotalSteps,
    int CorrectCount,
    int IncorrectCount,
    decimal Accuracy,
    int ElapsedSeconds);

public sealed record ActiveExerciseSession(
    Guid Id,
    Guid ExerciseId,
    string ExerciseName,
    string ExerciseType,
    ExerciseSessionStatus Status,
    DateTime StartTime,
    int CurrentStep,
    int TotalSteps,
    decimal ProgressPercentage);

public sealed record ExerciseSessionResult(
    Guid SessionId,
    Guid StudentId,
    Guid ExerciseId,
    int CorrectCount,
    int IncorrectCount,
    decimal? Accuracy,
    int TimeSpentSeconds,
    decimal? Score,
    int? WordsRead,
    [property: JsonPropertyName("rawWPM")] decimal? RawWPM,
    decimal? ComprehensionScore,
    [property: JsonPropertyName("weightedKDP")] decimal? WeightedKDP,
    int XpGained,
    IReadOnlyList<string> UnlockedBadges,
    bool LeveledUp,
    int? NewLevel,
    JsonElement DetailedResults,
    string Feedback,
    string? NextRecommendation,
    string MeasurementStatus = "Measured");

public interface ISpeedReadingExerciseSessions
{
    Task<StartExerciseSessionResponse> StartAsync(
        Guid studentId,
        StartExerciseSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<ExerciseActionValidationResponse> ValidateActionAsync(
        Guid studentId,
        Guid sessionId,
        ExerciseActionRequest request,
        CancellationToken cancellationToken = default);

    Task<ExerciseSessionResult> CompleteAsync(
        Guid studentId,
        Guid sessionId,
        CompleteExerciseSessionRequest request,
        CancellationToken cancellationToken = default);

    Task PauseAsync(Guid studentId, Guid sessionId, CancellationToken cancellationToken = default);

    Task ResumeAsync(Guid studentId, Guid sessionId, CancellationToken cancellationToken = default);

    Task<ExerciseSessionProgress> GetProgressAsync(
        Guid studentId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<ExerciseSessionDetails> GetDetailsAsync(
        Guid studentId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveExerciseSession>> GetActiveAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}

public static class SpeedReadingExerciseSessionRules
{
    public static decimal CalculateAccuracy(int correctCount, int incorrectCount)
    {
        var total = Math.Max(correctCount, 0) + Math.Max(incorrectCount, 0);
        return total == 0 ? 0 : Math.Round((decimal)Math.Max(correctCount, 0) / total * 100, 2);
    }

    public static int CalculateActiveSeconds(
        DateTime startTime,
        DateTime endTime,
        int totalPausedSeconds,
        DateTime? pausedAt,
        bool isPaused)
    {
        var elapsed = Math.Max(0, (endTime.ToUniversalTime() - startTime.ToUniversalTime()).TotalSeconds);
        elapsed -= Math.Max(0, totalPausedSeconds);
        if (isPaused && pausedAt.HasValue)
        {
            elapsed -= Math.Max(0, (endTime.ToUniversalTime() - pausedAt.Value.ToUniversalTime()).TotalSeconds);
        }

        return Math.Max(0, (int)Math.Round(elapsed));
    }

    public static int CalculateReadingSeconds(
        DateTime sessionStartTime,
        DateTime sessionEndTime,
        DateTime? readingStartTime,
        DateTime? readingEndTime,
        int pausedReadingSeconds)
    {
        var start = readingStartTime ?? sessionStartTime;
        var end = readingEndTime ?? sessionEndTime;
        var elapsed = Math.Max(0, (end.ToUniversalTime() - start.ToUniversalTime()).TotalSeconds);
        elapsed -= Math.Max(0, pausedReadingSeconds);
        return Math.Max(0, (int)Math.Round(elapsed));
    }

    public static decimal CalculateCompositeScore(decimal comprehensionScore, decimal? rawWpm)
    {
        var comprehension = Math.Clamp(comprehensionScore, 0, 100);
        if (rawWpm is not > 0)
        {
            return comprehension;
        }

        var speedComponent = Math.Min(rawWpm.Value / 5, 100) * 0.4m;
        return Math.Round(Math.Clamp((comprehension * 0.6m) + speedComponent, 0, 100), 2);
    }

    public static decimal? CalculateValidatedRawWpm(int wordsRead, int readingSeconds)
    {
        if (wordsRead <= 0 || readingSeconds < 3)
        {
            return null;
        }

        var rawWpm = (decimal)wordsRead / readingSeconds * 60;
        return rawWpm is < 20 or > 1_500
            ? null
            : Math.Round(rawWpm, 2);
    }

    public static SpeedReadingMeasurementStatus ResolveMeasurementStatus(
        int questionCount,
        int correctCount,
        int incorrectCount)
    {
        return questionCount > 0 || Math.Max(correctCount, 0) + Math.Max(incorrectCount, 0) > 0
            ? SpeedReadingMeasurementStatus.Measured
            : SpeedReadingMeasurementStatus.NotMeasured;
    }

    public static int CalculateXp(decimal score, decimal accuracy, int timeSpentSeconds)
    {
        if (score <= 0 && accuracy <= 0)
        {
            return 0;
        }

        var xp = (int)Math.Round(score * 0.1m, MidpointRounding.AwayFromZero);
        if (accuracy >= 90) xp += 50;
        else if (accuracy >= 80) xp += 30;
        else if (accuracy >= 70) xp += 10;
        if (timeSpentSeconds < 60 && accuracy >= 50) xp += 20;
        if (accuracy < 20 && xp < 10) return 0;
        return Math.Max(xp, 10);
    }
}
