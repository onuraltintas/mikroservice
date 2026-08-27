using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Gamification;

public sealed class Achievement : Entity
{
    private Achievement()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Tier { get; private set; } = string.Empty;
    public string IconUrl { get; private set; } = string.Empty;
    public string IconEmoji { get; private set; } = string.Empty;
    public string CriteriaType { get; private set; } = string.Empty;
    public string CriteriaValue { get; private set; } = "{}";
    public string? TriggerType { get; private set; }
    public int? TriggerValue { get; private set; }
    public bool IsRepeatable { get; private set; }
    public int XPReward { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static Achievement Create(
        Guid id,
        string name,
        string description,
        string category,
        string tier,
        string? iconUrl,
        string iconEmoji,
        string criteriaType,
        string criteriaValue,
        string? triggerType,
        int? triggerValue,
        bool isRepeatable,
        int xpReward,
        bool isActive,
        int sortOrder,
        Guid actorId,
        DateTime createdAt)
    {
        Validate(name, description, category, tier, iconEmoji, criteriaType, criteriaValue, triggerValue, xpReward, sortOrder);
        if (id == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Achievement identifiers are required.");
        return new Achievement
        {
            Id = id,
            Name = name.Trim(),
            Description = description.Trim(),
            Category = category.Trim(),
            Tier = tier.Trim(),
            IconUrl = iconUrl?.Trim() ?? string.Empty,
            IconEmoji = iconEmoji.Trim(),
            CriteriaType = criteriaType.Trim(),
            CriteriaValue = criteriaValue.Trim(),
            TriggerType = triggerType?.Trim(),
            TriggerValue = triggerValue,
            IsRepeatable = isRepeatable,
            XPReward = xpReward,
            IsActive = isActive,
            SortOrder = sortOrder,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = actorId.ToString()
        };
    }

    public static Achievement Import(
        Guid id,
        string name,
        string description,
        string category,
        string tier,
        string? iconUrl,
        string iconEmoji,
        string criteriaType,
        string criteriaValue,
        string? triggerType,
        int? triggerValue,
        bool isRepeatable,
        int xpReward,
        bool isActive,
        int sortOrder,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        Validate(name, description, category, tier, iconEmoji, criteriaType, criteriaValue, triggerValue, xpReward, sortOrder);
        if (id == Guid.Empty)
            throw new ArgumentException("Achievement id is required.", nameof(id));
        return new Achievement
        {
            Id = id,
            Name = name.Trim(),
            Description = description.Trim(),
            Category = category.Trim(),
            Tier = tier.Trim(),
            IconUrl = iconUrl?.Trim() ?? string.Empty,
            IconEmoji = iconEmoji.Trim(),
            CriteriaType = criteriaType.Trim(),
            CriteriaValue = criteriaValue.Trim(),
            TriggerType = triggerType?.Trim(),
            TriggerValue = triggerValue,
            IsRepeatable = isRepeatable,
            XPReward = xpReward,
            IsActive = isActive,
            SortOrder = sortOrder,
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
        string description,
        string category,
        string tier,
        string? iconUrl,
        string iconEmoji,
        string criteriaType,
        string criteriaValue,
        string? triggerType,
        int? triggerValue,
        bool isRepeatable,
        int xpReward,
        bool isActive,
        int sortOrder,
        Guid actorId,
        DateTime updatedAt)
    {
        Validate(name, description, category, tier, iconEmoji, criteriaType, criteriaValue, triggerValue, xpReward, sortOrder);
        if (actorId == Guid.Empty)
            throw new ArgumentException("Achievement actor is required.", nameof(actorId));
        Name = name.Trim();
        Description = description.Trim();
        Category = category.Trim();
        Tier = tier.Trim();
        IconUrl = iconUrl?.Trim() ?? string.Empty;
        IconEmoji = iconEmoji.Trim();
        CriteriaType = criteriaType.Trim();
        CriteriaValue = criteriaValue.Trim();
        TriggerType = triggerType?.Trim();
        TriggerValue = triggerValue;
        IsRepeatable = isRepeatable;
        XPReward = xpReward;
        IsActive = isActive;
        SortOrder = sortOrder;
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Achievement actor is required.", nameof(actorId));
        IsDeleted = true;
        IsActive = false;
        DeletedAt = EnsureUtc(deletedAt);
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    private static void Validate(
        string name,
        string description,
        string category,
        string tier,
        string iconEmoji,
        string criteriaType,
        string criteriaValue,
        int? triggerValue,
        int xpReward,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200
            || string.IsNullOrWhiteSpace(description) || description.Trim().Length > 500
            || string.IsNullOrWhiteSpace(category) || category.Trim().Length > 50
            || string.IsNullOrWhiteSpace(tier) || tier.Trim().Length > 50
            || iconEmoji.Trim().Length > 10
            || string.IsNullOrWhiteSpace(criteriaType) || criteriaType.Trim().Length > 100
            || string.IsNullOrWhiteSpace(criteriaValue) || criteriaValue.Length > 4_000
            || triggerValue is < 0 || xpReward is < 0 or > 1_000_000
            || sortOrder is < 0 or > 100_000)
            throw new ArgumentException("Achievement fields are invalid.");
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
