using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.LearningPaths;

public sealed class LearningPathTemplate : AggregateRoot
{
    private LearningPathTemplate()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public Guid? TargetAgeGroupConfigurationId { get; private set; }
    public string? Description { get; private set; }
    public int TotalNodes { get; private set; }
    public int EstimatedDays { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static LearningPathTemplate Create(
        Guid id,
        string name,
        Guid? targetAgeGroupConfigurationId,
        string? description,
        int estimatedDays,
        bool isActive,
        Guid actorId,
        DateTime createdAt)
    {
        Validate(name, estimatedDays);
        if (id == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Learning path identifiers are required.");

        return new LearningPathTemplate
        {
            Id = id,
            Name = name.Trim(),
            TargetAgeGroupConfigurationId = targetAgeGroupConfigurationId,
            Description = Normalize(description),
            TotalNodes = 0,
            EstimatedDays = estimatedDays,
            IsActive = isActive,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = actorId.ToString()
        };
    }

    public static LearningPathTemplate Import(
        Guid id,
        string name,
        Guid? targetAgeGroupConfigurationId,
        string? description,
        int totalNodes,
        int estimatedDays,
        bool isActive,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        Validate(name, estimatedDays);
        if (id == Guid.Empty)
            throw new ArgumentException("Learning path template id is required.", nameof(id));

        return new LearningPathTemplate
        {
            Id = id,
            Name = name.Trim(),
            TargetAgeGroupConfigurationId = targetAgeGroupConfigurationId,
            Description = Normalize(description),
            TotalNodes = Math.Max(totalNodes, 0),
            EstimatedDays = estimatedDays,
            IsActive = isActive,
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
        Guid? targetAgeGroupConfigurationId,
        string? description,
        int estimatedDays,
        bool isActive,
        Guid actorId,
        DateTime updatedAt)
    {
        Validate(name, estimatedDays);
        if (actorId == Guid.Empty)
            throw new ArgumentException("Learning path actor is required.", nameof(actorId));

        Name = name.Trim();
        TargetAgeGroupConfigurationId = targetAgeGroupConfigurationId;
        Description = Normalize(description);
        EstimatedDays = estimatedDays;
        IsActive = isActive;
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void SetTotalNodes(int totalNodes, Guid actorId, DateTime updatedAt)
    {
        if (totalNodes < 0 || actorId == Guid.Empty)
            throw new ArgumentException("Learning path node count is invalid.");
        TotalNodes = totalNodes;
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Learning path actor is required.", nameof(actorId));
        IsDeleted = true;
        IsActive = false;
        DeletedAt = EnsureUtc(deletedAt);
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    private static void Validate(string name, int estimatedDays)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Learning path name is invalid.", nameof(name));
        if (estimatedDays < 0 || estimatedDays > 3_650)
            throw new ArgumentOutOfRangeException(nameof(estimatedDays));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
