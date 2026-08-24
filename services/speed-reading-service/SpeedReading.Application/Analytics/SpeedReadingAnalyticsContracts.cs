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
}
