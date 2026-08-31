using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Profiles;

public sealed class SpeedReadingUserProfile : AggregateRoot
{
    private SpeedReadingUserProfile()
    {
    }

    public Guid UserId { get; private set; }
    public int CurrentLevel { get; private set; }
    public int TargetWPM { get; private set; }
    public decimal TargetComprehension { get; private set; }
    public int DailyGoalMinutes { get; private set; }
    public Guid? AgeGroupConfigurationId { get; private set; }
    public Guid? InstitutionId { get; private set; }
    public bool IsActive { get; private set; }

    public static SpeedReadingUserProfile CreateDefault(
        Guid id,
        Guid userId,
        DateTime createdAt,
        string? createdBy = null,
        int targetWpm = 150,
        decimal targetComprehension = 70)
    {
        if (id == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Speed Reading profile identifiers are required.");

        return new SpeedReadingUserProfile
        {
            Id = id,
            UserId = userId,
            CurrentLevel = 1,
            TargetWPM = Math.Max(targetWpm, 0),
            TargetComprehension = Math.Clamp(targetComprehension, 0, 100),
            IsActive = true,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy
        };
    }

    public static SpeedReadingUserProfile Import(
        Guid id,
        Guid userId,
        int currentLevel,
        int targetWpm,
        decimal targetComprehension,
        int dailyGoalMinutes,
        Guid? ageGroupConfigurationId,
        Guid? institutionId,
        bool isActive,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        var profile = CreateDefault(id, userId, createdAt, createdBy, targetWpm, targetComprehension);
        profile.CurrentLevel = Math.Max(currentLevel, 1);
        profile.DailyGoalMinutes = Math.Max(dailyGoalMinutes, 0);
        profile.AgeGroupConfigurationId = ageGroupConfigurationId;
        profile.InstitutionId = institutionId;
        profile.IsActive = isActive;
        profile.UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null;
        profile.UpdatedBy = updatedBy;
        return profile;
    }

    public void ApplyAssessment(
        int currentLevel,
        int targetWpm,
        decimal targetComprehension,
        Guid actorId,
        DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Profile actor is required.", nameof(actorId));

        CurrentLevel = Math.Max(currentLevel, 1);
        TargetWPM = Math.Max(targetWpm, 0);
        TargetComprehension = Math.Clamp(targetComprehension, 0, 100);
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    public void SkipAssessment(
        int targetWpm,
        decimal targetComprehension,
        Guid actorId,
        DateTime at)
    {
        ApplyAssessment(1, targetWpm, targetComprehension, actorId, at);
    }

    public void UpdateSettings(
        int currentLevel,
        int targetWpm,
        decimal targetComprehension,
        int dailyGoalMinutes,
        Guid? ageGroupConfigurationId,
        Guid actorId,
        DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Profile actor is required.", nameof(actorId));
        if (dailyGoalMinutes is < 5 or > 480)
            throw new ArgumentOutOfRangeException(nameof(dailyGoalMinutes), "Daily goal must be between 5 and 480 minutes");

        CurrentLevel = Math.Max(currentLevel, 1);
        TargetWPM = Math.Max(targetWpm, 0);
        TargetComprehension = Math.Clamp(targetComprehension, 0, 100);
        DailyGoalMinutes = dailyGoalMinutes;
        if (ageGroupConfigurationId.HasValue)
            AgeGroupConfigurationId = ageGroupConfigurationId;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
