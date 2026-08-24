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

public interface ILegacySpeedReadingAdminAnalytics
{
    Task<AdminPlatformUsageAnalytics> GetPlatformUsageAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);
}
