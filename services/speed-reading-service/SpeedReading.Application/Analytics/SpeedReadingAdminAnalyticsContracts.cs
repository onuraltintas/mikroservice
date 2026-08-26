namespace SpeedReading.Application.Analytics;

using EduPlatform.Shared.Contracts.Reporting;

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

public sealed record AdminInstitutionComparison(
    Guid InstitutionId,
    string InstitutionName,
    int TotalUsers,
    int ActiveUsers,
    int TotalStudents,
    int TotalTeachers,
    int TotalActivities,
    decimal AverageWpm,
    bool AverageWpmDataAvailable,
    decimal AverageComprehension,
    bool AverageComprehensionDataAvailable,
    decimal AveragePerformance,
    decimal EngagementRate);

public sealed record AdminTopInstitution(
    string InstitutionName,
    decimal AverageWpm,
    bool AverageWpmDataAvailable,
    decimal AverageComprehension,
    bool AverageComprehensionDataAvailable,
    int ActiveStudents,
    bool ActiveStudentsDataAvailable,
    int TotalActivities);

public sealed record AdminInstitutionAnalytics(
    DateTime DateFrom,
    DateTime DateTo,
    int TotalInstitutions,
    int ActiveInstitutions,
    int TotalUsers,
    int TotalStudents,
    int TotalTeachers,
    IReadOnlyList<AdminInstitutionComparison> InstitutionComparison,
    AdminAnalyticsChartData InstitutionComparisonChart,
    IReadOnlyList<AdminAnalyticsChartData> UsersByInstitution,
    IReadOnlyList<AdminAnalyticsChartData> ActivityByInstitution,
    IReadOnlyList<AdminAnalyticsChartData> PerformanceByInstitution,
    IReadOnlyList<AdminTopInstitution> TopInstitutions);

public sealed record SpeedReadingProgramPlatformStats(
    int TotalActiveStudents,
    decimal AverageSuccessRate,
    decimal AverageCurrentStreak,
    int TotalCompletedExercises);

public sealed record SpeedReadingProgramDistribution(
    string ProgramName,
    int StudentCount,
    decimal Percentage);

public sealed record SpeedReadingWeeklyProgress(
    int WeekNumber,
    decimal AverageProgress,
    decimal CompletionRate);

public sealed record SpeedReadingRecentStudentProgress(
    string StudentName,
    string StudentEmail,
    string ProgramName,
    int CurrentWeek,
    int CurrentDay,
    int CurrentStreak,
    int LongestStreak,
    decimal SuccessRate,
    int DifficultyLevel,
    DateTime LastActivityDate);

public sealed record SpeedReadingProgramAnalytics(
    SpeedReadingProgramPlatformStats PlatformStats,
    IReadOnlyList<SpeedReadingProgramDistribution> ProgramDistribution,
    IReadOnlyList<SpeedReadingWeeklyProgress> WeeklyProgress,
    IReadOnlyList<SpeedReadingRecentStudentProgress> RecentStudentProgress);

public sealed record SpeedReadingProgramAnalyticsRow(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    Guid ProgramTemplateId,
    string ProgramName,
    int CurrentWeek,
    int CurrentDay,
    int CurrentStreak,
    int LongestStreak,
    decimal SuccessRate,
    int DifficultyLevel,
    DateTime LastActivityDate,
    int DaysCompleted,
    int ExercisesCompleted,
    bool IsActive);

public static class SpeedReadingProgramAnalyticsCalculator
{
    public static SpeedReadingProgramAnalytics Calculate(
        IReadOnlyList<SpeedReadingProgramAnalyticsRow> rows)
    {
        var activeRows = rows.Where(item => item.IsActive).ToArray();
        var platformStats = new SpeedReadingProgramPlatformStats(
            activeRows.Select(item => item.UserId).Distinct().Count(),
            Round(activeRows.Select(item => item.SuccessRate).DefaultIfEmpty().Average()),
            Round(activeRows.Select(item => (decimal)item.CurrentStreak).DefaultIfEmpty().Average()),
            activeRows.Sum(item => item.ExercisesCompleted));

        var activeCount = activeRows.Length;
        var programDistribution = activeRows
            .GroupBy(item => new { item.ProgramTemplateId, item.ProgramName })
            .Select(group => new SpeedReadingProgramDistribution(
                string.IsNullOrWhiteSpace(group.Key.ProgramName) ? "Bilinmeyen Program" : group.Key.ProgramName,
                group.Select(item => item.UserId).Distinct().Count(),
                activeCount == 0 ? 0 : Math.Round((decimal)group.Count() / activeCount * 100, 1)))
            .OrderByDescending(item => item.StudentCount)
            .ThenBy(item => item.ProgramName)
            .ToArray();

        var weeklyProgress = activeRows
            .GroupBy(item => item.CurrentWeek)
            .OrderBy(group => group.Key)
            .Take(12)
            .Select(group => new SpeedReadingWeeklyProgress(
                group.Key,
                Round(group.Select(item => item.SuccessRate).Average()),
                Math.Round((decimal)group.Count(item => item.DaysCompleted >= item.CurrentWeek * 5) / group.Count() * 100, 1)))
            .ToArray();

        var recentStudentProgress = activeRows
            .OrderByDescending(item => item.LastActivityDate)
            .Take(20)
            .Select(item => new SpeedReadingRecentStudentProgress(
                BuildStudentName(item.FirstName, item.LastName),
                item.Email ?? string.Empty,
                string.IsNullOrWhiteSpace(item.ProgramName) ? "Bilinmeyen Program" : item.ProgramName,
                item.CurrentWeek,
                item.CurrentDay,
                item.CurrentStreak,
                item.LongestStreak,
                item.SuccessRate,
                item.DifficultyLevel,
                item.LastActivityDate))
            .ToArray();

        return new SpeedReadingProgramAnalytics(
            platformStats,
            programDistribution,
            weeklyProgress,
            recentStudentProgress);
    }

    private static decimal Round(decimal value) => Math.Round(value, 1);

    private static string BuildStudentName(string firstName, string lastName)
    {
        var name = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? "Bilinmeyen" : name;
    }
}

public interface ISpeedReadingInstitutionDirectory
{
    Task<SpeedReadingInstitutionScopeResponse> GetInstitutionsAsync(
        CancellationToken cancellationToken = default);
}

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

    Task<AdminInstitutionAnalytics> GetInstitutionAnalyticsAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingProgramAnalytics> GetProgramAnalyticsAsync(
        CancellationToken cancellationToken = default);
}
