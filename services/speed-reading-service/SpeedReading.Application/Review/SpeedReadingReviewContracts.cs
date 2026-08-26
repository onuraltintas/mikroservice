namespace SpeedReading.Application.Review;

public sealed record ReviewExerciseSummary(
    Guid ReviewItemId,
    Guid ExerciseId,
    string ExerciseTitle,
    string ExerciseDescription,
    int DifficultyLevel,
    string SeriesName,
    string SeriesTitle,
    string ExerciseType,
    DateTime FirstCompletedAt,
    DateTime? LastReviewedAt,
    DateTime NextReviewDate,
    int ReviewCount,
    int CurrentIntervalDays,
    double EasinessFactor,
    int DaysOverdue,
    double LastReviewScore,
    double AverageReviewScore,
    bool IsMastered,
    bool IsOverdue);

public sealed record ReviewStatisticsSummary(
    int TotalReviewItems,
    int TotalInReviewQueue,
    int DueToday,
    int Overdue,
    int ReviewedToday,
    int MasteredExercises,
    int DueThisWeek,
    double AverageReviewScore,
    double AverageInterval,
    int CurrentStreak,
    int TotalCompletedReviews,
    DateTime? NextReviewDate,
    DateTime? LastReviewDate);

public sealed record SubmitReviewResult(
    bool Success,
    string Message,
    DateTime NextReviewDate,
    int IntervalDays,
    bool IsMastered,
    double EasinessFactor);

public sealed record SubmitReviewRequest(double Score);

public sealed record ReviewHistoryItem(
    DateTime ReviewedAt,
    double Score,
    int IntervalDays,
    int ReviewNumber);

public sealed record AddReviewRequest(Guid ExerciseId, string? TrainingSeriesId);

public interface ISpeedReadingReview
{
    Task<IReadOnlyList<ReviewExerciseSummary>> GetDueAsync(Guid userId, Guid? seriesId, CancellationToken cancellationToken);
    Task<ReviewStatisticsSummary> GetStatisticsAsync(Guid userId, Guid? seriesId, CancellationToken cancellationToken);
    Task<SubmitReviewResult?> SubmitAsync(Guid userId, Guid reviewItemId, double score, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewHistoryItem>> GetHistoryAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken);
    Task<Guid?> AddAsync(Guid userId, AddReviewRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateDailyProgressAsync(Guid userId, Guid dailyProgressId, CancellationToken cancellationToken);
}
