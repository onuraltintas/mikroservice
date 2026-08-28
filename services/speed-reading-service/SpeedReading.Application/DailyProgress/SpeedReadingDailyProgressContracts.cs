namespace SpeedReading.Application.DailyProgress;

public sealed record DailyExerciseSummary(
    Guid ExerciseId,
    Guid ExerciseTypeId,
    string ExerciseTypeName,
    string Title,
    string Description,
    int DifficultyLevel,
    int EstimatedDurationMinutes,
    bool IsCompleted,
    DateTime? CompletedAt,
    int Order,
    string ConfigurationJson,
    bool IsBonus = false);

public sealed class CompleteDailyExerciseRequest
{
    // Source API names.
    public Guid? ExerciseLogId { get; init; }
    public decimal? Score { get; init; }
    public int? DurationSeconds { get; init; }

    // The current frontend sends these names. Both contracts are supported
    // during the migration so old clients do not silently lose completions.
    public Guid? ExerciseId { get; init; }
    public Guid? SessionId { get; init; }
    public decimal? SuccessRate { get; init; }
    public int? TimeSpentSeconds { get; init; }
    public string? MeasurementStatus { get; init; }

    public int CorrectCount { get; init; }
    public int IncorrectCount { get; init; }
    public int TotalAttempts { get; init; }
    public decimal AverageResponseTimeMs { get; init; }
    public decimal MedianResponseTimeMs { get; init; }
    public decimal StdDevResponseTimeMs { get; init; }
    public int PauseCount { get; init; }
    public int TotalPausedSeconds { get; init; }
    public string? DevicePlatform { get; init; }
    public string? ResultDataJson { get; init; }
}

public sealed record RecommendedProgramSummary(
    Guid TemplateId,
    string Name,
    string Description,
    int TotalDays,
    int TotalWeeks,
    string DifficultyRange,
    string RecommendationReason,
    string ProgramType);

public sealed record ProgramCompletionStats(
    int TotalDays,
    decimal AverageSuccessRate,
    int LongestStreak,
    int TotalExercises);

public sealed record CompleteDailyExerciseResponse(
    bool Success,
    string Message,
    bool DayCompleted,
    int CurrentDay,
    int CurrentWeek,
    int CurrentDifficultyLevel,
    bool DifficultyIncreased,
    int OldDifficultyLevel,
    bool WeekChanged,
    int OldWeek,
    int? NewDifficultyLevel,
    int CurrentStreak,
    int LongestStreak,
    bool ProgramCompleted,
    RecommendedProgramSummary? RecommendedNextProgram,
    ProgramCompletionStats? CompletionStats);

public sealed record StudentProgressSummary(
    int CurrentWeek,
    int CurrentDay,
    int CurrentDifficultyLevel,
    int ExercisesCompleted,
    decimal AverageSuccessRate,
    int CurrentStreak,
    int LongestStreak,
    DateTime? LastCompletionDate,
    string TemplateName,
    int TotalWeeks,
    int TotalDays,
    int TotalExercisesCompleted);

public sealed record DailyProgressSummary(
    Guid Id,
    int CurrentDay,
    int DaysCompleted,
    int ExercisesCompleted,
    int TotalExercises,
    int CompletedExercises,
    DateTime AssignedDate,
    decimal CurrentReadingSpeed,
    decimal InitialReadingSpeed,
    decimal AverageSuccessRate);

public sealed record WeeklyProgressSummary(
    int TotalExercises,
    int CompletedExercises,
    decimal AverageScore,
    int TotalMinutes,
    int ActiveDays);

public sealed record DailyProgressCalendarDay(
    DateTime Date,
    int TotalExercises,
    int CompletedExercises,
    decimal AverageScore);

public sealed record DailyProgressCalendar(
    int Month,
    int Year,
    IReadOnlyList<DailyProgressCalendarDay> Days);

public interface ISpeedReadingDailyProgress
{
    Task<IReadOnlyList<DailyExerciseSummary>> GetTodayExercisesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyExerciseSummary>> GetExercisesByDayAsync(
        Guid userId,
        int dayNumber,
        CancellationToken cancellationToken = default);

    Task<CompleteDailyExerciseResponse> CompleteExerciseAsync(
        Guid userId,
        CompleteDailyExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<DailyProgressSummary?> GetProgressSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<WeeklyProgressSummary> GetWeeklyStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DailyProgressCalendar> GetCalendarAsync(
        Guid userId,
        int? month,
        int? year,
        CancellationToken cancellationToken = default);
}

public static class SpeedReadingDailyProgressRules
{
    public static string ValidateIdempotencyKey(string? key)
    {
        var normalized = key?.Trim() ?? string.Empty;
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                "^[A-Za-z0-9._~-]{16,128}$"))
        {
            throw new ArgumentException(
                "Idempotency-Key must contain 16-128 letters, numbers, dots, underscores, hyphens or tildes.",
                nameof(key));
        }

        return normalized;
    }

    public static decimal ResolveScore(decimal? legacyScore, decimal? frontendSuccessRate)
    {
        var score = legacyScore ?? frontendSuccessRate
            ?? throw new ArgumentException("Score or SuccessRate is required.");

        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(legacyScore), "Score must be between 0 and 100.");
        }

        return score;
    }

    public static int ResolveDuration(int? legacyDuration, int? frontendDuration)
    {
        var duration = legacyDuration ?? frontendDuration
            ?? throw new ArgumentException("DurationSeconds or TimeSpentSeconds is required.");
        return ValidateDuration(duration);
    }

    public static int ValidateDuration(int duration)
    {
        if (duration <= 0 || duration > 24 * 60 * 60)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be between 1 second and 24 hours.");
        }

        return duration;
    }

    public static int CalculateNextStreak(
        DateTime? lastCompletionDate,
        DateTime completionDate,
        int currentStreak)
    {
        if (!lastCompletionDate.HasValue)
        {
            return 1;
        }

        var previousDate = lastCompletionDate.Value.ToUniversalTime().Date;
        var currentDate = completionDate.ToUniversalTime().Date;

        if (previousDate == currentDate)
        {
            return Math.Max(currentStreak, 1);
        }

        return previousDate == currentDate.AddDays(-1)
            ? Math.Max(currentStreak, 1) + 1
            : 1;
    }

    public static (int Week, int Day) GetWeekAndDay(int cumulativeDay)
    {
        if (cumulativeDay < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cumulativeDay));
        }

        return (((cumulativeDay - 1) / 7) + 1, ((cumulativeDay - 1) % 7) + 1);
    }
}
