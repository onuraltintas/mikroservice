using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Assignments;

/// <summary>
/// A student's membership and result state for an assignment.
/// </summary>
public sealed class StudentAssignment : Entity
{
    private StudentAssignment()
    {
    }

    public Guid AssignmentId { get; private set; }
    public Guid StudentId { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletionDate { get; private set; }
    public Guid? ResultId { get; private set; }
    public decimal? Score { get; private set; }
    public decimal? KeyPerformanceMetric { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static StudentAssignment Assign(
        Guid assignmentId,
        Guid studentId,
        Guid? id = null,
        DateTime? assignedAt = null,
        string? createdBy = null)
    {
        if (assignmentId == Guid.Empty)
            throw new ArgumentException("Assignment is required.", nameof(assignmentId));
        if (studentId == Guid.Empty)
            throw new ArgumentException("Student is required.", nameof(studentId));

        return new StudentAssignment
        {
            Id = id.GetValueOrDefault(Guid.NewGuid()),
            AssignmentId = assignmentId,
            StudentId = studentId,
            IsActive = true,
            CreatedAt = EnsureUtc(assignedAt ?? DateTime.UtcNow),
            CreatedBy = createdBy
        };
    }

    public static StudentAssignment Import(
        Guid id,
        Guid assignmentId,
        Guid studentId,
        bool isCompleted,
        DateTime? completionDate,
        Guid? resultId,
        decimal? score,
        decimal? keyPerformanceMetric,
        bool isActive,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        var assignment = Assign(assignmentId, studentId, id, createdAt);
        assignment.IsCompleted = isCompleted;
        assignment.CompletionDate = completionDate.HasValue ? EnsureUtc(completionDate.Value) : null;
        assignment.ResultId = resultId;
        assignment.Score = score;
        assignment.KeyPerformanceMetric = keyPerformanceMetric;
        assignment.IsActive = isActive;
        assignment.CreatedAt = EnsureUtc(createdAt);
        assignment.CreatedBy = createdBy;
        assignment.UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null;
        assignment.UpdatedBy = updatedBy;
        return assignment;
    }

    public void Complete(
        Guid resultId,
        decimal score,
        decimal keyPerformanceMetric,
        DateTime completedAt)
    {
        if (resultId == Guid.Empty)
            throw new ArgumentException("Assignment result is required.", nameof(resultId));
        if (!IsActive)
            throw new InvalidOperationException("An inactive student assignment cannot be completed.");
        if (IsCompleted)
            return;

        IsCompleted = true;
        CompletionDate = EnsureUtc(completedAt);
        ResultId = resultId;
        Score = score;
        KeyPerformanceMetric = keyPerformanceMetric;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
