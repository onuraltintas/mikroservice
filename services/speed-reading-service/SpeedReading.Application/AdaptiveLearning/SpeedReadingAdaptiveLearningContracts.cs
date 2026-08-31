namespace SpeedReading.Application.AdaptiveLearning;

public sealed record AdaptiveProfileSummary(
    Guid Id,
    Guid StudentId,
    string ProficiencyLevel,
    int CurrentDifficultyLevel,
    decimal AverageWpm,
    decimal AverageComprehension,
    int TotalReadingSessions,
    int TotalExerciseSessions,
    int CurrentStreak,
    int LongestStreak,
    DateTime? LastActiveDate,
    IReadOnlyDictionary<int, decimal> BloomPerformance,
    IReadOnlyDictionary<string, int> CategoryPreferences,
    IReadOnlyList<int> WeakAreas,
    int TotalMinutesSpent,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AdaptiveWeakAreaSummary(
    int BloomLevel,
    string BloomLevelName,
    decimal CurrentScore,
    string Recommendation);

public sealed record AdaptiveContentRecommendationSummary(
    Guid Id,
    Guid ReadingTextId,
    string Title,
    string Category,
    int DifficultyLevel,
    int WordCount,
    decimal RecommendationScore,
    string Reason,
    string RecommendationType,
    bool IsViewed,
    bool IsStarted,
    bool IsCompleted,
    DateTime CreatedAt,
    DateTime ExpiresAt);

public sealed record AdaptiveDailyGoalSummary(
    Guid Id,
    Guid StudentId,
    DateTime Date,
    int TargetMinutes,
    int TargetReadingSessions,
    int TargetExercises,
    int ActualMinutes,
    int ActualReadingSessions,
    int ActualExercises,
    bool IsAchieved,
    DateTime? AchievedAt,
    decimal AchievementPercentage,
    string MotivationalMessage,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AdaptiveRecentResult(
    decimal Wpm,
    decimal ComprehensionScore,
    DateTime CompletedAt);

public sealed record AdaptiveDashboardSummary(
    AdaptiveProfileSummary Profile,
    AdaptiveDailyGoalSummary TodayGoal,
    IReadOnlyList<AdaptiveRecentResult> RecentResults,
    int CurrentStreak,
    int TotalMinutes,
    int TotalSessions,
    decimal OverallProgress);

public sealed record UpdateAfterAdaptiveSessionRequest(
    Guid? ReadingTextId,
    int TimeSpentSeconds,
    decimal Wpm,
    decimal ComprehensionScore,
    IReadOnlyDictionary<int, bool>? BloomAnswers);

public sealed record UpdateAdaptiveDailyProgressRequest(
    int MinutesSpent,
    bool SessionCompleted,
    bool ExerciseCompleted);

public sealed record UpdateAdaptiveProfileSettingsRequest(
    int CurrentLevel,
    int TargetWPM,
    decimal TargetComprehension,
    int DailyGoalMinutes,
    Guid? AgeGroupConfigurationId);

public interface ISpeedReadingAdaptiveLearning
{
    Task<AdaptiveProfileSummary> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateProfileSettingsAsync(
        Guid userId,
        UpdateAdaptiveProfileSettingsRequest request,
        CancellationToken cancellationToken = default);
    Task<AdaptiveDashboardSummary> GetDashboardAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdaptiveWeakAreaSummary>> GetWeakAreasAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdaptiveContentRecommendationSummary>> GetRecommendationsAsync(Guid userId, int count, CancellationToken cancellationToken = default);
    Task<AdaptiveDailyGoalSummary> GetDailyGoalAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAfterSessionAsync(Guid userId, UpdateAfterAdaptiveSessionRequest request, CancellationToken cancellationToken = default);
    Task<AdaptiveDailyGoalSummary> UpdateDailyProgressAsync(Guid userId, UpdateAdaptiveDailyProgressRequest request, CancellationToken cancellationToken = default);
}
