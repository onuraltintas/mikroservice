namespace SpeedReading.Application.Analytics;

public sealed record AdminAnalyticsChartSeries(
    string Name,
    decimal Value);

public sealed record AdminAnalyticsChartData(
    string Name,
    IReadOnlyList<AdminAnalyticsChartSeries> Series);

public sealed record AdminPlatformPopularContent(
    string Title,
    string Type,
    int UsageCount);

public sealed record AdminPlatformInstitutionActivity(
    string Name,
    int ActiveUserCount,
    int TotalActionCount);

public sealed record AdminPlatformUsageAnalytics(
    DateTime DateFrom,
    DateTime DateTo,
    int TotalUsers,
    int ActiveUsers,
    int NewUsers,
    bool NewUserDataAvailable,
    int TotalActivities,
    int TotalReadingSessions,
    decimal AverageSessionDuration,
    decimal UserGrowthRate,
    bool UserGrowthRateDataAvailable,
    decimal EngagementRate,
    decimal RetentionRate,
    IReadOnlyList<AdminAnalyticsChartData> UserGrowth,
    IReadOnlyList<AdminAnalyticsChartData> DailyActiveUsers,
    IReadOnlyList<AdminAnalyticsChartData> ActivityVolume,
    IReadOnlyList<AdminAnalyticsChartData> HourlyActivity,
    IReadOnlyList<AdminPlatformPopularContent> PopularContent,
    IReadOnlyList<AdminPlatformInstitutionActivity> TopInstitutions,
    IReadOnlyDictionary<string, int> FeatureUsageStats);

public sealed record AdminContentUsageData(
    Guid ContentId,
    string ContentType,
    string Title,
    int UsageCount,
    decimal AverageScore,
    decimal AverageComprehension);

public sealed record AdminReadingLevelAnalysis(
    int DifficultyLevel,
    int TotalReads,
    decimal AverageWpm,
    decimal AverageComprehension);

public sealed record AdminExerciseTypeAnalysis(
    string ExerciseTypeName,
    int TotalCompletions,
    int ActiveStudents,
    decimal AverageScore,
    string PerformanceLevel);

public sealed record AdminContentAnalysisAnalytics(
    DateTime DateFrom,
    DateTime DateTo,
    int TotalExercises,
    int TotalReadingTexts,
    int TotalTrainingSeries,
    int TotalProgramTemplates,
    int TotalAssignments,
    bool AssignmentDataAvailable,
    IReadOnlyList<AdminContentUsageData> MostUsedContent,
    IReadOnlyList<AdminContentUsageData> LeastUsedContent,
    IReadOnlyList<AdminAnalyticsChartData> PerformanceByContentType,
    IReadOnlyList<AdminAnalyticsChartData> EngagementByContentType,
    IReadOnlyList<string> ContentGaps,
    IReadOnlyList<string> PopularTopics,
    IReadOnlyList<AdminReadingLevelAnalysis> ReadingAnalysis,
    IReadOnlyList<AdminExerciseTypeAnalysis> ExerciseAnalysis,
    IReadOnlyList<AdminAnalyticsChartData> ReadingPerformanceChart,
    IReadOnlyList<AdminAnalyticsChartData> ExerciseFrequencyChart);

public sealed record AdminSystemAlert(
    string Severity,
    string AlertType,
    string Message,
    DateTime DetectedAt);

public sealed record AdminSystemHealthAnalytics(
    DateTime DateFrom,
    DateTime DateTo,
    decimal OverallHealthScore,
    bool OverallHealthDataAvailable,
    string HealthStatus,
    decimal AveragePlatformWpm,
    decimal AveragePlatformComprehension,
    decimal UserSatisfactionScore,
    bool UserSatisfactionDataAvailable,
    int TotalExercisesCompleted,
    int TotalQuestionsAnswered,
    decimal SuccessRate,
    decimal ErrorRate,
    bool ErrorRateDataAvailable,
    IReadOnlyList<AdminAnalyticsChartData> HealthTrend,
    IReadOnlyList<AdminAnalyticsChartData> PerformanceTrend,
    IReadOnlyList<AdminSystemAlert> SystemAlerts,
    bool SystemAlertsDataAvailable);

public interface ILegacySpeedReadingAdminAnalytics
{
    Task<AdminPlatformUsageAnalytics> GetPlatformUsageAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<AdminContentAnalysisAnalytics> GetContentAnalysisAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<AdminSystemHealthAnalytics> GetSystemHealthAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);
}
