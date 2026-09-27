using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Profiles;

public sealed class SpeedReadingUserProfile : AggregateRoot
{
    private SpeedReadingUserProfile()
    {
    }

    public Guid UserId { get; private set; }
    public int CurrentLevel { get; private set; }
    public int? GradeLevel { get; private set; }
    public int TargetWPM { get; private set; }
    public decimal TargetComprehension { get; private set; }
    public int DailyGoalMinutes { get; private set; }
    public Guid? AgeGroupConfigurationId { get; private set; }
    public DateTime? DateOfBirth { get; private set; }
    public string? LearningStyle { get; private set; }
    public Guid? InstitutionId { get; private set; }
    public bool IsActive { get; private set; }
    public string? HistoricalDisplayName { get; private set; }
    public string? HistoricalEmail { get; private set; }

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
        string? updatedBy,
        string? historicalDisplayName = null,
        string? historicalEmail = null,
        DateTime? dateOfBirth = null,
        string? learningStyle = null,
        int? gradeLevel = null)
    {
        ValidateGradeLevel(gradeLevel);
        var profile = CreateDefault(id, userId, createdAt, createdBy, targetWpm, targetComprehension);
        profile.CurrentLevel = Math.Max(currentLevel, 1);
        profile.GradeLevel = gradeLevel;
        profile.DailyGoalMinutes = Math.Max(dailyGoalMinutes, 0);
        profile.AgeGroupConfigurationId = ageGroupConfigurationId;
        profile.InstitutionId = institutionId;
        profile.IsActive = isActive;
        profile.UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null;
        profile.UpdatedBy = updatedBy;
        profile.RefreshHistoricalDisplay(historicalDisplayName, historicalEmail);
        profile.DateOfBirth = dateOfBirth.HasValue ? EnsureUtc(dateOfBirth.Value) : null;
        profile.LearningStyle = NormalizeLearningStyle(learningStyle);
        return profile;
    }

    public void SetGradeLevel(int? gradeLevel, Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Profile actor is required.", nameof(actorId));
        ValidateGradeLevel(gradeLevel);

        GradeLevel = gradeLevel;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    public void RefreshHistoricalDisplay(string? displayName, string? email)
    {
        HistoricalDisplayName = Normalize(displayName, 200);
        HistoricalEmail = Normalize(email, 320);
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

    public void ApplyAdaptiveLevel(int level, Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Profile actor is required.", nameof(actorId));

        CurrentLevel = Math.Max(level, 1);
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    public void UpdateSettings(
        int currentLevel,
        int targetWpm,
        decimal targetComprehension,
        int dailyGoalMinutes,
        Guid? ageGroupConfigurationId,
        Guid actorId,
        DateTime at,
        DateTime? dateOfBirth = null,
        string? learningStyle = null)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Profile actor is required.", nameof(actorId));
        if (dailyGoalMinutes is < 5 or > 480)
            throw new ArgumentOutOfRangeException(nameof(dailyGoalMinutes), "Daily goal must be between 5 and 480 minutes");

        var normalizedAt = EnsureUtc(at);
        var normalizedBirthDate = dateOfBirth.HasValue ? EnsureUtc(dateOfBirth.Value) : (DateTime?)null;
        if (normalizedBirthDate.HasValue && normalizedBirthDate.Value.Date > normalizedAt.Date)
            throw new ArgumentOutOfRangeException(nameof(dateOfBirth), "Date of birth cannot be in the future.");
        var normalizedLearningStyle = NormalizeLearningStyle(learningStyle);

        CurrentLevel = Math.Max(currentLevel, 1);
        TargetWPM = Math.Max(targetWpm, 0);
        TargetComprehension = Math.Clamp(targetComprehension, 0, 100);
        DailyGoalMinutes = dailyGoalMinutes;
        if (ageGroupConfigurationId.HasValue)
            AgeGroupConfigurationId = ageGroupConfigurationId;
        if (normalizedBirthDate.HasValue)
            DateOfBirth = normalizedBirthDate;
        if (normalizedLearningStyle is not null)
            LearningStyle = normalizedLearningStyle;
        UpdatedAt = normalizedAt;
        UpdatedBy = actorId.ToString();
    }

    private static string? NormalizeLearningStyle(string? value)
    {
        if (value is null)
            return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "visual" or "auditory" or "kinesthetic"
            ? normalized
            : throw new ArgumentException("Learning style is not supported.", nameof(value));
    }

    private static void ValidateGradeLevel(int? gradeLevel)
    {
        if (gradeLevel is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(gradeLevel), "School grade must be between 1 and 12.");
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
