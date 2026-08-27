using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.AgeGroups;

public sealed class AgeGroupConfiguration : AggregateRoot
{
    private AgeGroupConfiguration()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public int MinAge { get; private set; }
    public int? MaxAge { get; private set; }
    public int RecommendedWPM { get; private set; }
    public int MinWPM { get; private set; }
    public int MaxWPM { get; private set; }
    public int RecommendedComprehension { get; private set; }
    public int RecommendedDailyMinutes { get; private set; }
    public int DefaultDifficultyLevel { get; private set; }
    public int OrderIndex { get; private set; }
    public bool IsActive { get; private set; }
    public string? Description { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static AgeGroupConfiguration Create(
        Guid id,
        string name,
        string displayName,
        int minAge,
        int? maxAge,
        int minWpm,
        int recommendedWpm,
        int maxWpm,
        int recommendedComprehension,
        int recommendedDailyMinutes,
        int defaultDifficultyLevel,
        int orderIndex,
        bool isActive,
        string? description,
        Guid actorId,
        DateTime createdAt)
    {
        if (id == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Age group identifiers are required.");

        Validate(name, displayName, minAge, maxAge, minWpm, recommendedWpm, maxWpm,
            recommendedComprehension, recommendedDailyMinutes, defaultDifficultyLevel);

        return new AgeGroupConfiguration
        {
            Id = id,
            Name = name.Trim(),
            DisplayName = displayName.Trim(),
            MinAge = minAge,
            MaxAge = maxAge,
            MinWPM = minWpm,
            RecommendedWPM = recommendedWpm,
            MaxWPM = maxWpm,
            RecommendedComprehension = recommendedComprehension,
            RecommendedDailyMinutes = recommendedDailyMinutes,
            DefaultDifficultyLevel = defaultDifficultyLevel,
            OrderIndex = orderIndex,
            IsActive = isActive,
            Description = Normalize(description),
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = actorId.ToString()
        };
    }

    public static AgeGroupConfiguration Import(
        Guid id,
        string name,
        string displayName,
        int minAge,
        int? maxAge,
        int minWpm,
        int recommendedWpm,
        int maxWpm,
        int recommendedComprehension,
        int recommendedDailyMinutes,
        int defaultDifficultyLevel,
        int orderIndex,
        bool isActive,
        string? description,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        Validate(name, displayName, minAge, maxAge, minWpm, recommendedWpm, maxWpm,
            recommendedComprehension, recommendedDailyMinutes, defaultDifficultyLevel);

        return new AgeGroupConfiguration
        {
            Id = id,
            Name = name.Trim(),
            DisplayName = displayName.Trim(),
            MinAge = minAge,
            MaxAge = maxAge,
            MinWPM = minWpm,
            RecommendedWPM = recommendedWpm,
            MaxWPM = maxWpm,
            RecommendedComprehension = recommendedComprehension,
            RecommendedDailyMinutes = recommendedDailyMinutes,
            DefaultDifficultyLevel = defaultDifficultyLevel,
            OrderIndex = orderIndex,
            IsActive = isActive,
            Description = Normalize(description),
            IsDeleted = isDeleted,
            DeletedAt = deletedAt.HasValue ? EnsureUtc(deletedAt.Value) : null,
            DeletedBy = deletedBy,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    public void Update(
        string name,
        string displayName,
        int minAge,
        int? maxAge,
        int minWpm,
        int recommendedWpm,
        int maxWpm,
        int recommendedComprehension,
        int recommendedDailyMinutes,
        int defaultDifficultyLevel,
        int orderIndex,
        bool isActive,
        string? description,
        Guid actorId,
        DateTime updatedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Age group actor is required.", nameof(actorId));
        Validate(name, displayName, minAge, maxAge, minWpm, recommendedWpm, maxWpm,
            recommendedComprehension, recommendedDailyMinutes, defaultDifficultyLevel);

        Name = name.Trim();
        DisplayName = displayName.Trim();
        MinAge = minAge;
        MaxAge = maxAge;
        MinWPM = minWpm;
        RecommendedWPM = recommendedWpm;
        MaxWPM = maxWpm;
        RecommendedComprehension = recommendedComprehension;
        RecommendedDailyMinutes = recommendedDailyMinutes;
        DefaultDifficultyLevel = defaultDifficultyLevel;
        OrderIndex = orderIndex;
        IsActive = isActive;
        Description = Normalize(description);
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Age group actor is required.", nameof(actorId));

        IsDeleted = true;
        IsActive = false;
        DeletedAt = EnsureUtc(deletedAt);
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    private static void Validate(
        string name,
        string displayName,
        int minAge,
        int? maxAge,
        int minWpm,
        int recommendedWpm,
        int maxWpm,
        int recommendedComprehension,
        int recommendedDailyMinutes,
        int defaultDifficultyLevel)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Name is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 200)
            throw new ArgumentException("DisplayName is required and must not exceed 200 characters.");
        if (minAge is < 0 or > 150 || (maxAge.HasValue && (maxAge.Value < minAge || maxAge.Value > 150)))
            throw new ArgumentException("Age range is invalid.");
        if (minWpm < 0 || recommendedWpm < minWpm || maxWpm < recommendedWpm)
            throw new ArgumentException("WPM values must satisfy MinWPM <= RecommendedWPM <= MaxWPM.");
        if (recommendedComprehension is < 0 or > 100)
            throw new ArgumentException("RecommendedComprehension must be between 0 and 100.");
        if (recommendedDailyMinutes is < 0 or > 1440)
            throw new ArgumentException("RecommendedDailyMinutes must be between 0 and 1440.");
        if (defaultDifficultyLevel is < 1 or > 5)
            throw new ArgumentException("DefaultDifficultyLevel must be between 1 and 5.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
