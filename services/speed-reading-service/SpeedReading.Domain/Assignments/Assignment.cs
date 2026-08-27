using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Assignments;

/// <summary>
/// An assignment owned by the Speed Reading bounded context. Teacher and
/// student identifiers refer to Identity-owned users and are not cross-DB
/// foreign keys.
/// </summary>
public sealed class Assignment : AggregateRoot
{
    private Assignment()
    {
    }

    public Guid TeacherId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public Guid? ReadingTextId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime DueDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Assignment Create(
        Guid teacherId,
        Guid exerciseId,
        Guid? readingTextId,
        string title,
        string? description,
        DateTime dueDate,
        Guid? id = null,
        DateTime? createdAt = null,
        string? createdBy = null)
    {
        if (teacherId == Guid.Empty)
            throw new ArgumentException("Assignment teacher is required.", nameof(teacherId));
        if (exerciseId == Guid.Empty)
            throw new ArgumentException("Assignment exercise is required.", nameof(exerciseId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Assignment title is required.", nameof(title));

        return new Assignment
        {
            Id = id.GetValueOrDefault(Guid.NewGuid()),
            TeacherId = teacherId,
            ExerciseId = exerciseId,
            ReadingTextId = readingTextId,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            DueDate = EnsureUtc(dueDate),
            IsActive = true,
            CreatedAt = EnsureUtc(createdAt ?? DateTime.UtcNow),
            CreatedBy = createdBy
        };
    }

    public static Assignment Import(
        Guid id,
        Guid teacherId,
        Guid exerciseId,
        Guid? readingTextId,
        string title,
        string? description,
        DateTime dueDate,
        bool isActive,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        var assignment = Create(
            teacherId,
            exerciseId,
            readingTextId,
            title,
            description,
            dueDate,
            id,
            createdAt,
            createdBy);
        assignment.IsActive = isActive;
        assignment.CreatedAt = EnsureUtc(createdAt);
        assignment.CreatedBy = createdBy;
        assignment.UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null;
        assignment.UpdatedBy = updatedBy;
        return assignment;
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

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
