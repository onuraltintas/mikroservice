namespace SpeedReading.Application.Analytics;

public sealed record StudentAnalyticsDailyPoint(
    DateOnly Date,
    int ReadingSessions,
    int ExerciseCount,
    int ReadingMinutes,
    decimal AverageWpm,
    decimal AverageComprehension,
    decimal AverageSuccessRate);

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
    IReadOnlyList<StudentAnalyticsDailyPoint> Daily);

public interface ILegacySpeedReadingAnalytics
{
    Task<StudentAnalyticsSummary> GetStudentSummaryAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);
}
