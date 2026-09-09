using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Sessions;

/// <summary>
/// Server-owned lifecycle for the standalone reading endpoint.
/// The attempt identifier is reused as the resulting reading-session id so
/// completion is naturally idempotent and cannot be replayed with another user.
/// </summary>
public sealed class StudentReadingAttempt : Entity
{
    private StudentReadingAttempt()
    {
    }

    public Guid UserId { get; private set; }
    public Guid ReadingTextId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public static StudentReadingAttempt Start(
        Guid id,
        Guid userId,
        Guid readingTextId,
        DateTime startedAt)
    {
        if (id == Guid.Empty || userId == Guid.Empty || readingTextId == Guid.Empty)
            throw new ArgumentException("Student reading attempt identifiers are required.");

        var utcStartedAt = EnsureUtc(startedAt);
        return new StudentReadingAttempt
        {
            Id = id,
            UserId = userId,
            ReadingTextId = readingTextId,
            StartedAt = utcStartedAt,
            CreatedAt = utcStartedAt,
            CreatedBy = userId.ToString()
        };
    }

    public void Complete(DateTime completedAt)
    {
        if (CompletedAt.HasValue)
            return;

        var utcCompletedAt = EnsureUtc(completedAt);
        if (utcCompletedAt < StartedAt)
            throw new ArgumentException("Completion cannot precede the attempt start.", nameof(completedAt));

        CompletedAt = utcCompletedAt;
        UpdatedAt = utcCompletedAt;
        UpdatedBy = UserId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
