using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Programs;

public sealed class DailyExerciseLog : Entity
{
    private DailyExerciseLog()
    {
    }

    public Guid UserId { get; private set; }
    public Guid StudentProgramProgressId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public Guid ExerciseTypeId { get; private set; }
    public int DayNumber { get; private set; }
    public int WeekNumber { get; private set; }
    public int DifficultyLevel { get; private set; }
    public DateTime CompletedDate { get; private set; }
    public int TimeSpentSeconds { get; private set; }
    public decimal SuccessRate { get; private set; }
    public bool IsPassed { get; private set; }
    public string ResultDataJson { get; private set; } = "{}";
    public int AttemptNumber { get; private set; }
    public bool IsRetry { get; private set; }
    public string DevicePlatform { get; private set; } = string.Empty;
    public int CorrectCount { get; private set; }
    public int IncorrectCount { get; private set; }
    public int TotalAttempts { get; private set; }
    public decimal? AverageWPM { get; private set; }
    public decimal? AverageComprehension { get; private set; }
    public int DayOfWeek { get; private set; }
    public TimeSpan TimeOfDay { get; private set; }
    public decimal AverageResponseTimeMs { get; private set; }
    public decimal MedianResponseTimeMs { get; private set; }
    public decimal StdDevResponseTimeMs { get; private set; }
    public int PauseCount { get; private set; }
    public int TotalPausedSeconds { get; private set; }
    public decimal PerformanceTrend { get; private set; }
    public bool IsPersonalBest { get; private set; }
    public decimal PreviousAverageScore { get; private set; }
    public int CurrentStreak { get; private set; }
    public decimal EngagementScore { get; private set; }
    public decimal FrustrationScore { get; private set; }
    public decimal LearningRate { get; private set; }
    public decimal ConsistencyScore { get; private set; }

    public static DailyExerciseLog Import(
        Guid id,
        Guid userId,
        Guid studentProgramProgressId,
        Guid exerciseId,
        Guid exerciseTypeId,
        int dayNumber,
        int weekNumber,
        int difficultyLevel,
        DateTime completedDate,
        int timeSpentSeconds,
        decimal successRate,
        bool isPassed,
        string resultDataJson,
        int attemptNumber,
        bool isRetry,
        string devicePlatform,
        int correctCount,
        int incorrectCount,
        int totalAttempts,
        decimal? averageWpm,
        decimal? averageComprehension,
        int dayOfWeek,
        TimeSpan timeOfDay,
        decimal averageResponseTimeMs,
        decimal medianResponseTimeMs,
        decimal stdDevResponseTimeMs,
        int pauseCount,
        int totalPausedSeconds,
        decimal performanceTrend,
        bool isPersonalBest,
        decimal previousAverageScore,
        int currentStreak,
        decimal engagementScore,
        decimal frustrationScore,
        decimal learningRate,
        decimal consistencyScore,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || userId == Guid.Empty || studentProgramProgressId == Guid.Empty
            || exerciseId == Guid.Empty || exerciseTypeId == Guid.Empty)
        {
            throw new ArgumentException("Daily exercise identifiers are required.");
        }

        return new DailyExerciseLog
        {
            Id = id,
            UserId = userId,
            StudentProgramProgressId = studentProgramProgressId,
            ExerciseId = exerciseId,
            ExerciseTypeId = exerciseTypeId,
            DayNumber = dayNumber,
            WeekNumber = weekNumber,
            DifficultyLevel = difficultyLevel,
            CompletedDate = EnsureUtc(completedDate),
            TimeSpentSeconds = timeSpentSeconds,
            SuccessRate = successRate,
            IsPassed = isPassed,
            ResultDataJson = string.IsNullOrWhiteSpace(resultDataJson) ? "{}" : resultDataJson,
            AttemptNumber = attemptNumber,
            IsRetry = isRetry,
            DevicePlatform = devicePlatform?.Trim() ?? string.Empty,
            CorrectCount = correctCount,
            IncorrectCount = incorrectCount,
            TotalAttempts = totalAttempts,
            AverageWPM = averageWpm,
            AverageComprehension = averageComprehension,
            DayOfWeek = dayOfWeek,
            TimeOfDay = timeOfDay,
            AverageResponseTimeMs = averageResponseTimeMs,
            MedianResponseTimeMs = medianResponseTimeMs,
            StdDevResponseTimeMs = stdDevResponseTimeMs,
            PauseCount = pauseCount,
            TotalPausedSeconds = totalPausedSeconds,
            PerformanceTrend = performanceTrend,
            IsPersonalBest = isPersonalBest,
            PreviousAverageScore = previousAverageScore,
            CurrentStreak = currentStreak,
            EngagementScore = engagementScore,
            FrustrationScore = frustrationScore,
            LearningRate = learningRate,
            ConsistencyScore = consistencyScore,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
