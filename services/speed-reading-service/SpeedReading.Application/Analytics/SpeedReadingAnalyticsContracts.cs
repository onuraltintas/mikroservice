namespace SpeedReading.Application.Analytics;

public sealed record StudentAnalyticsDailyPoint(
    DateOnly Date,
    int ReadingSessions,
    int ExerciseCount,
    int ReadingMinutes,
    decimal AverageWpm,
    decimal AverageComprehension,
    decimal AverageSuccessRate);

public sealed record StudentAnalyticsMilestone(
    Guid Id,
    string Title,
    string Description,
    DateTime EarnedAt,
    string Type,
    string Icon);

public sealed record StudentAnalyticsTrendPoint(DateOnly Date, decimal Value);

public sealed record StudentAnalyticsBenchmark(
    decimal StudentValue,
    decimal InstitutionAverage,
    decimal PlatformAverage,
    string PerformanceLevel);

public sealed record StudentAnalyticsCategoryPoint(
    string CategoryName,
    decimal Value,
    int QuestionsAttempted,
    int CorrectAnswers,
    string PerformanceLevel);

public sealed record StudentAnalyticsQuestionTypePoint(
    string Type,
    decimal Value,
    int QuestionsAttempted,
    int CorrectAnswers);

public sealed record StudentReadingSpeedAnalytics(
    Guid UserId,
    DateTime DateFrom,
    DateTime DateTo,
    decimal CurrentWpm,
    decimal AverageWpm,
    decimal MedianWpm,
    decimal MinWpm,
    decimal MaxWpm,
    decimal StandardDeviation,
    decimal ImprovementRate,
    IReadOnlyList<StudentAnalyticsTrendPoint> Trend,
    IReadOnlyList<StudentAnalyticsCategoryPoint> Categories,
    StudentAnalyticsBenchmark Benchmark,
    int SessionsBelow200Wpm,
    int Sessions200To400Wpm,
    int SessionsAbove400Wpm,
    IReadOnlyList<string> Recommendations);

public sealed record StudentComprehensionAnalytics(
    Guid UserId,
    DateTime DateFrom,
    DateTime DateTo,
    decimal CurrentComprehension,
    decimal AverageComprehension,
    decimal MaxComprehension,
    decimal MinComprehension,
    decimal ImprovementRate,
    IReadOnlyList<StudentAnalyticsTrendPoint> Trend,
    IReadOnlyList<StudentAnalyticsCategoryPoint> Categories,
    IReadOnlyList<StudentAnalyticsQuestionTypePoint> QuestionTypes,
    int TotalQuestionsAttempted,
    int CorrectAnswers,
    decimal SuccessRate,
    StudentAnalyticsBenchmark Benchmark,
    IReadOnlyList<string> WeakAreas,
    IReadOnlyList<string> StrongAreas);

public sealed record StudentSeriesItem(
    Guid SeriesId,
    string SeriesName,
    decimal Progress,
    int DaysCompleted,
    int TotalDays,
    DateTime StartedAt,
    DateTime? LastActivityAt,
    decimal AverageScore);

public sealed record StudentSeriesMilestone(
    Guid Id,
    string Title,
    string Description,
    DateTime EarnedAt,
    Guid? SeriesId,
    string SeriesName,
    string Type,
    string Icon);

public sealed record StudentSeriesPerformanceStats(
    decimal AverageCompletionTime,
    decimal AverageScore,
    decimal ConsistencyScore,
    string EngagementLevel);

public sealed record StudentSeriesAnalytics(
    Guid UserId,
    DateTime DateFrom,
    DateTime DateTo,
    bool DataAvailable,
    string? UnavailableReason,
    int TotalSeriesStarted,
    int SeriesCompleted,
    int SeriesInProgress,
    int TotalMilestones,
    IReadOnlyList<StudentSeriesItem> ActiveSeries,
    IReadOnlyList<StudentAnalyticsTrendPoint> CompletionTimeline,
    IReadOnlyList<StudentSeriesMilestone> Milestones,
    StudentSeriesPerformanceStats PerformanceStats);

public sealed record StudentActivityDistributionPoint(
    string Label,
    int Value);

public sealed record StudentActivityHeatmapPoint(
    DateOnly Date,
    int Value,
    int Level);

public sealed record StudentActivityStreak(
    int Days,
    int LongestStreak,
    DateTime? LastActivityDate,
    bool IsActive);

public sealed record StudentActivityStudyTime(
    int TotalMinutes,
    decimal AverageSessionLength,
    int TotalSessions,
    int MostActiveHour,
    string MostActiveDay,
    decimal Consistency);

public sealed record StudentActivityAnalytics(
    Guid UserId,
    DateTime DateFrom,
    DateTime DateTo,
    bool DataAvailable,
    string? UnavailableReason,
    StudentActivityStreak CurrentStreak,
    IReadOnlyList<StudentActivityHeatmapPoint> Heatmap,
    IReadOnlyList<StudentActivityDistributionPoint> HourlyDistribution,
    IReadOnlyList<StudentActivityDistributionPoint> DailyDistribution,
    StudentActivityStudyTime StudyTime);

public sealed record StudentAnalyticsSummary(
    Guid UserId,
    DateTime DateFrom,
    DateTime DateTo,
    int ReadingSessions,
    decimal AverageWpm,
    decimal AverageComprehension,
    int TotalReadingMinutes,
    int BestWpm,
    int ExercisesCompleted,
    int ExercisesPassed,
    decimal AverageSuccessRate,
    decimal LatestWpm,
    decimal LatestComprehension,
    int CurrentLevel,
    int CurrentStreak,
    int LongestStreak,
    long TotalXp,
    int MilestonesEarned,
    int DailyGoalMinutes,
    decimal GoalCompletionRate,
    IReadOnlyList<StudentAnalyticsMilestone> RecentMilestones,
    IReadOnlyList<StudentAnalyticsDailyPoint> Daily);

public interface ILegacySpeedReadingAnalytics
{
    Task<StudentAnalyticsSummary> GetStudentSummaryAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<StudentReadingSpeedAnalytics> GetStudentReadingSpeedAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<StudentComprehensionAnalytics> GetStudentComprehensionAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<StudentSeriesAnalytics> GetStudentSeriesAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<StudentActivityAnalytics> GetStudentActivityAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);
}
