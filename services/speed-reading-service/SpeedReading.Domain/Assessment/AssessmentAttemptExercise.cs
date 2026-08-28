using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Assessment;

/// <summary>
/// Immutable form item selected for an assessment attempt. The snapshot keeps
/// the exact content used by the measurement stable after it has started.
/// </summary>
public sealed class AssessmentAttemptExercise : Entity
{
    private AssessmentAttemptExercise()
    {
    }

    public Guid AssessmentAttemptId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public Guid? ReadingTextId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public int OrderIndex { get; private set; }
    public string ContentSnapshotJson { get; private set; } = string.Empty;

    public static AssessmentAttemptExercise Pin(
        Guid id,
        Guid assessmentAttemptId,
        Guid exerciseId,
        Guid? readingTextId,
        string role,
        int orderIndex,
        string contentSnapshotJson,
        DateTime createdAt,
        string? createdBy)
    {
        if (id == Guid.Empty || assessmentAttemptId == Guid.Empty || exerciseId == Guid.Empty)
            throw new ArgumentException("Assessment form item identifiers are required.");
        if (string.IsNullOrWhiteSpace(role) || role.Trim().Length > 50)
            throw new ArgumentException("Assessment form item role is required and must not exceed 50 characters.", nameof(role));
        if (orderIndex is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(orderIndex));
        if (string.IsNullOrWhiteSpace(contentSnapshotJson))
            throw new ArgumentException("Assessment form item content snapshot is required.", nameof(contentSnapshotJson));

        return new AssessmentAttemptExercise
        {
            Id = id,
            AssessmentAttemptId = assessmentAttemptId,
            ExerciseId = exerciseId,
            ReadingTextId = readingTextId,
            Role = role.Trim().ToLowerInvariant(),
            OrderIndex = orderIndex,
            ContentSnapshotJson = contentSnapshotJson.Trim(),
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy
        };
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
