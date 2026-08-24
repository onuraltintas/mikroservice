namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyAchievement : LegacyBaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string IconEmoji { get; set; } = string.Empty;
    public string CriteriaType { get; set; } = string.Empty;
    public string CriteriaValue { get; set; } = "{}";
    public string? TriggerType { get; set; }
    public int? TriggerValue { get; set; }
    public bool IsRepeatable { get; set; }
    public int XPReward { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class LegacyUserAchievement : LegacyBaseEntity
{
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public DateTime UnlockedAt { get; set; }
    public bool IsShowcased { get; set; }
    public int? ShowcaseOrder { get; set; }
}

internal sealed class LegacyUserGamification : LegacyBaseEntity
{
    public Guid UserId { get; set; }
    public long TotalXP { get; set; }
    public int CurrentLevel { get; set; }
    public int CurrentLevelXP { get; set; }
    public int NextLevelXP { get; set; }
    public string LevelTitle { get; set; } = string.Empty;
    public string LevelIcon { get; set; } = string.Empty;
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public int StreakFreezeCount { get; set; }
    public int TotalActivitiesCompleted { get; set; }
    public int TotalReadingMinutes { get; set; }
}

internal sealed class LegacyUser
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? InstitutionId { get; set; }
    public bool IsDeleted { get; set; }
}
