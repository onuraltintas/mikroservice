using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.LearningPaths;

public sealed class StudentLearningNodeProgress : Entity
{
    private StudentLearningNodeProgress()
    {
    }

    public Guid StudentId { get; private set; }
    public Guid NodeId { get; private set; }
    public string Status { get; private set; } = "Pending";
    public decimal? Score { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static StudentLearningNodeProgress Import(
        Guid id,
        Guid studentId,
        Guid nodeId,
        string status,
        decimal? score,
        DateTime? completedAt,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || studentId == Guid.Empty || nodeId == Guid.Empty || string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Student node progress fields are invalid.");
        if (score is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(score));

        return new StudentLearningNodeProgress
        {
            Id = id,
            StudentId = studentId,
            NodeId = nodeId,
            Status = status.Trim(),
            Score = score,
            CompletedAt = completedAt.HasValue ? EnsureUtc(completedAt.Value) : null,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    public void Complete(decimal? score, Guid actorId, DateTime completedAt)
    {
        if (actorId == Guid.Empty || score is < 0 or > 100)
            throw new ArgumentException("Student node completion is invalid.");
        Status = "Completed";
        Score = score;
        CompletedAt ??= EnsureUtc(completedAt);
        UpdatedAt = EnsureUtc(completedAt);
        UpdatedBy = actorId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
