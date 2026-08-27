using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.LearningPaths;

public sealed class StudentLearningPathProgress : Entity
{
    private StudentLearningPathProgress()
    {
    }

    public Guid StudentId { get; private set; }
    public Guid TemplateId { get; private set; }
    public Guid? CurrentNodeId { get; private set; }
    public decimal Progress { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static StudentLearningPathProgress Import(
        Guid id,
        Guid studentId,
        Guid templateId,
        Guid? currentNodeId,
        decimal progress,
        bool isCompleted,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || studentId == Guid.Empty || templateId == Guid.Empty)
            throw new ArgumentException("Student learning path identifiers are required.");
        if (progress is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(progress));

        return new StudentLearningPathProgress
        {
            Id = id,
            StudentId = studentId,
            TemplateId = templateId,
            CurrentNodeId = currentNodeId,
            Progress = progress,
            IsCompleted = isCompleted,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    public void UpdateState(decimal progress, bool isCompleted, Guid? currentNodeId, Guid actorId, DateTime updatedAt)
    {
        if (actorId == Guid.Empty || progress is < 0 or > 100)
            throw new ArgumentException("Student learning path state is invalid.");
        Progress = progress;
        IsCompleted = isCompleted;
        CurrentNodeId = currentNodeId;
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
