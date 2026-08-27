using SpeedReading.Application.Content;

namespace SpeedReading.Application.Gamification;

public sealed record AwardXpRequest(int Amount, string Source, Guid? SourceId = null);

public sealed record UpdateGamificationStreakRequest(DateTime ActivityDate, int DurationMinutes);

public sealed record UpdateGamificationShowcaseRequest(IReadOnlyList<Guid> AchievementIds);

public sealed record GamificationLevelUpResult(
    bool LeveledUp,
    int OldLevel,
    int NewLevel,
    int OldTier,
    int NewTier,
    bool IsTierChange,
    string LevelTitle,
    string LevelIcon,
    IReadOnlyList<AchievementSummary> AchievementsUnlocked,
    long TotalXP,
    int CurrentLevelXP,
    int NextLevelXP);

public sealed record GamificationSummary(
    Guid UserId,
    long TotalXP,
    int CurrentLevel,
    int CurrentLevelXP,
    int NextLevelXP,
    string LevelTitle,
    string LevelIcon,
    int CurrentStreak,
    int LongestStreak,
    DateTime? LastActivityDate,
    int StreakFreezeCount,
    int TotalActivitiesCompleted,
    int TotalReadingMinutes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AchievementSummary(
    Guid Id,
    string Name,
    string Description,
    string Category,
    string Tier,
    string IconUrl,
    string IconEmoji,
    string CriteriaType,
    string CriteriaValue,
    string? TriggerType,
    int? TriggerValue,
    bool IsRepeatable,
    int XpReward,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt);

public sealed record UserAchievementSummary(
    Guid Id,
    Guid UserId,
    Guid AchievementId,
    DateTime UnlockedAt,
    bool IsShowcased,
    int? ShowcaseOrder,
    AchievementSummary Achievement);

public sealed record LeaderboardEntry(
    Guid UserId,
    string UserName,
    int Rank,
    decimal Value,
    int Level,
    string LevelTitle,
    string LevelIcon,
    IReadOnlyList<AchievementSummary> ShowcasedAchievements);

public sealed record AchievementAdminSummary(
    Guid Id,
    string Name,
    string Description,
    string Category,
    string Tier,
    string IconUrl,
    string IconEmoji,
    string CriteriaType,
    string CriteriaValue,
    string? TriggerType,
    int? TriggerValue,
    bool IsRepeatable,
    int XpReward,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int UnlockedByUsersCount);

public sealed record AchievementAdminStats(
    int TotalCount,
    int ActiveCount,
    int InactiveCount,
    int BronzeCount,
    int SilverCount,
    int GoldCount,
    int DiamondCount,
    IReadOnlyDictionary<string, int> CategoryCounts);

public sealed record CreateAchievementRequest(
    string Name,
    string Description,
    string Category,
    string Tier,
    string? IconUrl,
    string IconEmoji,
    string CriteriaType,
    string CriteriaValue,
    string? TriggerType,
    int? TriggerValue,
    bool IsRepeatable,
    int XpReward,
    bool IsActive,
    int SortOrder);

public sealed record UpdateAchievementRequest(
    string Name,
    string Description,
    string Category,
    string Tier,
    string? IconUrl,
    string IconEmoji,
    string CriteriaType,
    string CriteriaValue,
    string? TriggerType,
    int? TriggerValue,
    bool IsRepeatable,
    int XpReward,
    bool IsActive,
    int SortOrder);

public interface ILegacySpeedReadingGamification
{
    Task<GamificationLevelUpResult> AwardXpAsync(
        Guid userId,
        AwardXpRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AchievementSummary>> CheckAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateShowcaseAsync(
        Guid userId,
        IReadOnlyList<Guid> achievementIds,
        CancellationToken cancellationToken = default);

    Task UpdateStreakAsync(
        Guid userId,
        UpdateGamificationStreakRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UseStreakFreezeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<GamificationSummary> GetUserGamificationAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AchievementSummary>> GetAchievementsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserAchievementSummary>> GetUserAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        string type,
        int skip,
        int take,
        Guid requestingUserId,
        bool isGlobalAdministrator,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<AchievementAdminSummary>> GetAchievementAdminPageAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        string? category,
        string? tier,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<AchievementAdminSummary> GetAchievementAdminAsync(
        Guid achievementId,
        CancellationToken cancellationToken = default);

    Task<AchievementAdminStats> GetAchievementAdminStatsAsync(
        CancellationToken cancellationToken = default);
}

public interface ISpeedReadingGamificationAdminWriter
{
    Task<AchievementAdminSummary> CreateAchievementAsync(
        Guid actorId,
        CreateAchievementRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<AchievementAdminSummary> UpdateAchievementAsync(
        Guid actorId,
        Guid achievementId,
        UpdateAchievementRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteAchievementAsync(
        Guid actorId,
        Guid achievementId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public static class SpeedReadingGamificationRules
{
    public static int CalculateLevel(long totalXp) =>
        SpeedReading.Domain.Gamification.GamificationRules.CalculateLevel(totalXp);

    public static int GetCurrentLevelXp(long totalXp, int level) =>
        SpeedReading.Domain.Gamification.GamificationRules.GetCurrentLevelXp(totalXp, level);

    public static int GetTier(int level) =>
        SpeedReading.Domain.Gamification.GamificationRules.GetTier(level);

    public static string GetLevelTitle(int level) =>
        SpeedReading.Domain.Gamification.GamificationRules.GetLevelTitle(level);

    public static string GetLevelIcon(int level) =>
        SpeedReading.Domain.Gamification.GamificationRules.GetLevelIcon(level);

    public static int CalculateNextStreak(DateTime? lastActivityDate, DateTime activityDate, int currentStreak) =>
        SpeedReading.Domain.Gamification.GamificationRules.CalculateNextStreak(
            lastActivityDate, activityDate, currentStreak);
}
