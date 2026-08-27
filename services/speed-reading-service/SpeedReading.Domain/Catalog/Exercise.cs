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

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
