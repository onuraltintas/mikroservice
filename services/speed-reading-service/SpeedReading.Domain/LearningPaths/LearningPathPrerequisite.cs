using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.LearningPaths;

public sealed class LearningPathPrerequisite : Entity
{
    private LearningPathPrerequisite()
    {
    }

    public Guid NodeId { get; private set; }
    public Guid PrerequisiteNodeId { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static LearningPathPrerequisite Import(
        Guid id,
        Guid nodeId,
        Guid prerequisiteNodeId,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || nodeId == Guid.Empty || prerequisiteNodeId == Guid.Empty || nodeId == prerequisiteNodeId)
            throw new ArgumentException("Learning path prerequisite fields are invalid.");

        return new LearningPathPrerequisite
        {
            Id = id,
            NodeId = nodeId,
            PrerequisiteNodeId = prerequisiteNodeId,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
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

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
