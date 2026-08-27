namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyStudentLearningProfileSource
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public int CurrentDifficultyLevel { get; set; }
    public double AverageWpm { get; set; }
    public double AverageComprehension { get; set; }
    public int TotalReadingSessions { get; set; }
    public int TotalExerciseSessions { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTime LastActiveDate { get; set; }
    public string? BloomPerformanceJson { get; set; }
    public string? CategoryPreferencesJson { get; set; }
    public string? WeakAreasJson { get; set; }
    public string? RecentlyReadMaterialIdsJson { get; set; }
    public int TotalMinutesSpent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class LegacyContentRecommendationSource
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ReadingTextId { get; set; }
    public int RecommendationScore { get; set; }
    public string? Reason { get; set; }
    public string? RecommendationType { get; set; }
    public bool IsViewed { get; set; }
    public bool IsStarted { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? ViewedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal sealed class LegacyDailyGoalSource
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public DateTime Date { get; set; }
    public int TargetMinutes { get; set; }
    public int TargetReadingSessions { get; set; }
    public int TargetExercises { get; set; }
    public int ActualMinutes { get; set; }
    public int ActualReadingSessions { get; set; }
    public int ActualExercises { get; set; }
    public bool IsAchieved { get; set; }
    public DateTime? AchievedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
