using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.LearningPaths;

public sealed class LearningPathNodeContent : Entity
{
    private LearningPathNodeContent()
    {
    }

    public Guid NodeId { get; private set; }
    public Guid? ExerciseId { get; private set; }
    public Guid? ReadingTextId { get; private set; }
    public string? Description { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static LearningPathNodeContent Import(
        Guid id,
        Guid nodeId,
        Guid? exerciseId,
        Guid? readingTextId,
        string? description,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || nodeId == Guid.Empty || (!exerciseId.HasValue && !readingTextId.HasValue))
            throw new ArgumentException("Learning path node content fields are invalid.");

        return new LearningPathNodeContent
        {
            Id = id,
            NodeId = nodeId,
            ExerciseId = exerciseId,
            ReadingTextId = readingTextId,
            Description = Normalize(description),
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    public void Update(Guid? exerciseId, Guid? readingTextId, string? description, Guid actorId, DateTime updatedAt)
    {
        if (actorId == Guid.Empty || (!exerciseId.HasValue && !readingTextId.HasValue))
            throw new ArgumentException("Learning path node content fields are invalid.");
        ExerciseId = exerciseId;
        ReadingTextId = readingTextId;
        Description = Normalize(description);
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Learning path actor is required.", nameof(actorId));
        IsDeleted = true;
        DeletedAt = EnsureUtc(deletedAt);
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
