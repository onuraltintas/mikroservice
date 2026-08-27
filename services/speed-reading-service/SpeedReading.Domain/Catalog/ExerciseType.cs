using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Catalog;

public sealed class ExerciseType : AggregateRoot
{
    private ExerciseType()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string IconName { get; private set; } = string.Empty;
    public string ColorCode { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string EngineType { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static ExerciseType Create(
        Guid id,
        string name,
        string displayName,
        string engineType,
        Guid? categoryId = null,
        string? description = null,
        string? iconName = null,
        string? colorCode = null,
        int sortOrder = 0)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Exercise type id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Exercise type name and display name are required.");
        if (string.IsNullOrWhiteSpace(engineType))
            throw new ArgumentException("Exercise engine type is required.", nameof(engineType));

        return new ExerciseType
        {
            Id = id,
            Name = name.Trim(),
            DisplayName = displayName.Trim(),
            EngineType = engineType.Trim(),
            CategoryId = categoryId,
            Description = description?.Trim() ?? string.Empty,
            IconName = iconName?.Trim() ?? string.Empty,
            ColorCode = colorCode?.Trim() ?? string.Empty,
            SortOrder = sortOrder
        };
    }

    public static ExerciseType Import(
        Guid id,
        string name,
        string displayName,
        string engineType,
        Guid? categoryId,
        string? description,
        string? iconName,
        string? colorCode,
        int sortOrder,
        bool isActive,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        var exerciseType = Create(
            id,
            name,
            displayName,
            engineType,
            categoryId,
            description,
            iconName,
            colorCode,
            sortOrder);
        exerciseType.IsActive = isActive;
        exerciseType.CreatedAt = createdAt;
        exerciseType.CreatedBy = createdBy;
        exerciseType.UpdatedAt = updatedAt;
        exerciseType.UpdatedBy = updatedBy;
        return exerciseType;
    }

    public void Update(
        string name,
        string displayName,
        string engineType,
        Guid? categoryId,
        string? description,
        string? iconName,
        string? colorCode,
        int sortOrder,
        bool isActive,
        Guid actorId,
        DateTime updatedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Exercise type actor is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(engineType))
            throw new ArgumentException("Exercise type fields are required.");

        Name = name.Trim();
        DisplayName = displayName.Trim();
        EngineType = engineType.Trim();
        CategoryId = categoryId;
        Description = description?.Trim() ?? string.Empty;
        IconName = iconName?.Trim() ?? string.Empty;
        ColorCode = colorCode?.Trim() ?? string.Empty;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = updatedAt.Kind == DateTimeKind.Utc ? updatedAt : updatedAt.ToUniversalTime();
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Exercise type actor is required.", nameof(actorId));
        IsDeleted = true;
        IsActive = false;
        DeletedAt = deletedAt.Kind == DateTimeKind.Utc ? deletedAt : deletedAt.ToUniversalTime();
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }
}
