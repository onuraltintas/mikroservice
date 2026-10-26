namespace CourseManagement.Domain;

public enum PackageMovementType { Granted = 1, LessonConsumed = 2, LessonRestored = 3 }

public sealed class PackageMovement
{
    private PackageMovement() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid FeeAgreementId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid? LessonSessionId { get; private set; }
    public Guid? ReversalOfMovementId { get; private set; }
    public PackageMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static PackageMovement Grant(Guid id, FeeAgreement agreement, DateTimeOffset now) => new()
    {
        Id = id, WorkspaceId = agreement.WorkspaceId, FeeAgreementId = agreement.Id, StudentId = agreement.StudentId,
        Type = PackageMovementType.Granted, Quantity = agreement.PackageSessions ?? 0, OccurredAt = now.ToUniversalTime()
    };

    public static PackageMovement Consume(Guid id, FeeAgreement agreement, Guid lessonSessionId, DateTimeOffset now) => new()
    {
        Id = id, WorkspaceId = agreement.WorkspaceId, FeeAgreementId = agreement.Id, StudentId = agreement.StudentId,
        LessonSessionId = lessonSessionId, Type = PackageMovementType.LessonConsumed, Quantity = -1, OccurredAt = now.ToUniversalTime()
    };

    public static PackageMovement Restore(Guid id, PackageMovement original, DateTimeOffset now)
    {
        if (id == Guid.Empty || original.Type != PackageMovementType.LessonConsumed)
            throw new ArgumentException("Only a completed package lesson can be restored.", nameof(original));
        return new PackageMovement
        {
            Id = id, WorkspaceId = original.WorkspaceId, FeeAgreementId = original.FeeAgreementId,
            StudentId = original.StudentId, ReversalOfMovementId = original.Id,
            Type = PackageMovementType.LessonRestored, Quantity = 1, OccurredAt = now.ToUniversalTime()
        };
    }
}
