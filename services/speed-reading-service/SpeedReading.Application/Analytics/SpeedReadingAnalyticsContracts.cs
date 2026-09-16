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

public sealed record StudentAnalyticsBloomLevelPoint(
    int Level,
    string Label,
    decimal Value,
    int QuestionsAttempted,
    int CorrectAnswers);

public sealed record ReadingQuestionTypeAggregate(
    int QuestionType,
    int QuestionsAttempted,
    int CorrectAnswers);

public sealed record ReadingQuestionBloomAggregate(
    int BloomLevel,
    int QuestionsAttempted,
    int CorrectAnswers);

public static class ReadingQuestionAnalyticsRules
{
    public static IReadOnlyList<StudentAnalyticsQuestionTypePoint> SummarizeQuestionTypes(
        IEnumerable<ReadingQuestionTypeAggregate> aggregates)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        return aggregates
            .Where(item => item.QuestionType is >= 1 and <= 3 && item.QuestionsAttempted > 0)
            .GroupBy(item => item.QuestionType)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var attempted = group.Sum(item => item.QuestionsAttempted);
                var correct = Math.Clamp(group.Sum(item => item.CorrectAnswers), 0, attempted);
                return new StudentAnalyticsQuestionTypePoint(
                    QuestionTypeLabel(group.Key),
                    Math.Round(correct * 100m / attempted, 2),
                    attempted,
                    correct);
            })
            .ToList();
    }

    public static string QuestionTypeLabel(int questionType) => questionType switch
    {
        1 => "Gerçek Anlam",
        2 => "Çıkarım",
        3 => "Değerlendirme",
        _ => "Bilinmeyen"
    };

    public static IReadOnlyList<StudentAnalyticsBloomLevelPoint> SummarizeBloomLevels(
        IEnumerable<ReadingQuestionBloomAggregate> aggregates)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        return aggregates
            .Where(item => item.BloomLevel is >= 1 and <= 6 && item.QuestionsAttempted > 0)
            .GroupBy(item => item.BloomLevel)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var attempted = group.Sum(item => item.QuestionsAttempted);
                var correct = Math.Clamp(group.Sum(item => item.CorrectAnswers), 0, attempted);
                return new StudentAnalyticsBloomLevelPoint(
                    group.Key,
                    BloomLevelLabel(group.Key),
                    Math.Round(correct * 100m / attempted, 2),
                    attempted,
                    correct);
            })
            .ToList();
    }

    public static string BloomLevelLabel(int bloomLevel) => bloomLevel switch
    {
        1 => "Hatırlama",
        2 => "Anlama",
        3 => "Uygulama",
        4 => "Analiz",
        5 => "Değerlendirme",
        6 => "Yaratma",
        _ => "Bilinmeyen"
    };
}

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
    IReadOnlyList<StudentAnalyticsBloomLevelPoint> BloomLevels,
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

public sealed record StudentActivityDetail(
    DateTime CompletedAt,
    string ActivityType,
    Guid ContentId,
    string ContentTitle,
    string? ExerciseTypeName,
    int DifficultyLevel,
    int DurationSeconds,
    decimal? Wpm,
    decimal? Comprehension,
    decimal? SuccessRate,
    bool IsMeasured,
    bool IsPassed);

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
    IReadOnlyList<StudentActivityDetail> RecentActivities,
    StudentActivityStudyTime StudyTime);

public sealed record StudentProgramState(
    Guid ProgramId,
    string ProgramName,
    int CurrentDay,
    int CurrentWeek,
    int DifficultyLevel,
    int AdaptiveDifficultyOffset,
    int DaysCompleted,
    int TotalDays,
    decimal AverageSuccessRate,
    DateTime? LastActivityAt,
    bool IsActive);

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
    StudentProgramState? ProgramState,
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
