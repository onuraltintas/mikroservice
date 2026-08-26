namespace SpeedReading.Application.ContentFeedback;

public sealed record SaveContentFeedbackRequest(
    Guid ContentId,
    string ContentType,
    int? Rating,
    bool IsLiked,
    bool IsBookmarked,
    string? SkipReason,
    decimal CompletionRate,
    int TimeSpentSeconds,
    int ExpectedTimeSeconds,
    decimal? ComprehensionScore,
    decimal? ExerciseScore,
    int RetryCount,
    int InteractionCount,
    int PauseCount,
    decimal? AbandonedAtPercentage,
    string DeviceType,
    string? ContentCategory,
    int? ContentDifficultyLevel);

public sealed record UpdateContentFeedbackRequest(
    int? Rating,
    bool? IsLiked,
    bool? IsBookmarked);

public sealed record FeedbackCategorySummary(
    string Category,
    int Count,
    decimal AverageScore);

public sealed record FeedbackOptimalHourSummary(
    int Hour,
    decimal AverageScore,
    int SessionCount);

public sealed record FeedbackWeakAreaSummary(
    string Category,
    decimal AverageScore,
    int TotalAttempts,
    string Recommendation,
    string ImprovementNeeded);

public sealed record ContentFeedbackAnalyticsSummary(
    int TotalFeedbacks,
    decimal AverageCompletionRate,
    decimal AveragePerformanceScore,
    decimal AverageEngagementScore,
    int TotalLikes,
    int TotalBookmarks,
    int TotalSkips,
    IReadOnlyList<FeedbackCategorySummary> TopCategories,
    IReadOnlyList<FeedbackOptimalHourSummary> OptimalHours,
    IReadOnlyDictionary<string, int> ContentTypeBreakdown,
    IReadOnlyList<FeedbackWeakAreaSummary> WeakAreas);

public sealed record RecommendedContentSummary(
    Guid Id,
    string Title,
    string Description,
    string ContentType,
    string? Category,
    int? DifficultyLevel,
    decimal RecommendationScore,
    string ScoreReason);

public interface ISpeedReadingContentFeedback
{
    Task<Guid> SaveFeedbackAsync(Guid userId, SaveContentFeedbackRequest request, CancellationToken cancellationToken = default);
    Task<ContentFeedbackAnalyticsSummary> GetAnalyticsAsync(Guid userId, string? contentType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecommendedContentSummary>> GetRecommendedContentsAsync(Guid userId, string contentType, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetOptimalStudyHoursAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetContentsNeedingRetryAsync(Guid userId, string contentType, CancellationToken cancellationToken = default);
    Task<bool> UpdateExplicitFeedbackAsync(Guid userId, Guid contentId, string contentType, UpdateContentFeedbackRequest request, CancellationToken cancellationToken = default);
}
