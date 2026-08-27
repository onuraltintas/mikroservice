using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Catalog;

/// <summary>
/// Exercise catalog entry owned by the Speed Reading bounded context.
/// User and age-group identifiers are references to Identity-owned data; no
/// cross-database foreign keys are created for them.
/// </summary>
public sealed class Exercise : AggregateRoot
{
    private Exercise()
    {
    }

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string TypeCode { get; private set; } = string.Empty;
    public Guid ExerciseTypeId { get; private set; }
    public int DifficultyLevel { get; private set; }
    public string ConfigurationJson { get; private set; } = "{}";
    public Guid? TargetAgeGroupId { get; private set; }
    public Guid CreatorId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static Exercise Create(
        string title,
        string typeCode,
        string configurationJson,
        int difficultyLevel,
        Guid creatorId,
        Guid exerciseTypeId,
        Guid? id = null,
        string? description = null,
        Guid? targetAgeGroupId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Exercise title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(typeCode))
            throw new ArgumentException("Exercise type code is required.", nameof(typeCode));
        if (creatorId == Guid.Empty)
            throw new ArgumentException("Exercise creator is required.", nameof(creatorId));
        if (exerciseTypeId == Guid.Empty)
            throw new ArgumentException("Exercise type is required.", nameof(exerciseTypeId));
        if (difficultyLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(difficultyLevel));

        var exercise = new Exercise
        {
            Id = id.GetValueOrDefault(Guid.NewGuid()),
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            TypeCode = typeCode.Trim(),
            ExerciseTypeId = exerciseTypeId,
            DifficultyLevel = difficultyLevel,
            ConfigurationJson = string.IsNullOrWhiteSpace(configurationJson) ? "{}" : configurationJson,
            TargetAgeGroupId = targetAgeGroupId,
            CreatorId = creatorId,
            IsActive = true
        };

        return exercise;
    }

    public static Exercise Import(
        Guid id,
        string title,
        string typeCode,
        string configurationJson,
        int difficultyLevel,
        Guid creatorId,
        Guid exerciseTypeId,
        DateTime createdAt,
        Guid? targetAgeGroupId,
        string? description,
        bool isActive,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        var exercise = Create(
            title,
            typeCode,
            configurationJson,
            difficultyLevel,
            creatorId,
            exerciseTypeId,
            id,
            description,
            targetAgeGroupId);
        exercise.IsActive = isActive;
        exercise.CreatedAt = createdAt;
        exercise.CreatedBy = createdBy;
        exercise.UpdatedAt = updatedAt;
        exercise.UpdatedBy = updatedBy;
        return exercise;
    }

    public void Update(
        string title,
        string? description,
        string typeCode,
        string configurationJson,
        int difficultyLevel,
        Guid exerciseTypeId,
        Guid? targetAgeGroupId,
        Guid actorId,
        DateTime updatedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Exercise actor is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(typeCode)
            || exerciseTypeId == Guid.Empty || difficultyLevel < 0)
            throw new ArgumentException("Exercise fields are invalid.");

        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        TypeCode = typeCode.Trim();
        ConfigurationJson = string.IsNullOrWhiteSpace(configurationJson) ? "{}" : configurationJson;
        DifficultyLevel = difficultyLevel;
        ExerciseTypeId = exerciseTypeId;
        TargetAgeGroupId = targetAgeGroupId;
        UpdatedAt = updatedAt.Kind == DateTimeKind.Utc ? updatedAt : updatedAt.ToUniversalTime();
        UpdatedBy = actorId.ToString();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Exercise actor is required.", nameof(actorId));
        IsDeleted = true;
        IsActive = false;
        DeletedAt = deletedAt.Kind == DateTimeKind.Utc ? deletedAt : deletedAt.ToUniversalTime();
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = actorId.ToString();
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
