using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

public sealed class CoachingLegalHold : AggregateRoot
{
    public Guid SubjectUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid PlacedByUserId { get; private set; }
    public DateTime PlacedAt { get; private set; }
    public Guid? ReleasedByUserId { get; private set; }
    public DateTime? ReleasedAt { get; private set; }

    private CoachingLegalHold() { }

    public static CoachingLegalHold Place(
        Guid subjectUserId,
        string reason,
        Guid placedByUserId,
        DateTime placedAt)
    {
        if (subjectUserId == Guid.Empty || placedByUserId == Guid.Empty)
            throw new ArgumentException("Subject and operator are required.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2_000)
            throw new ArgumentException("Legal hold reason is required and must not exceed 2000 characters.", nameof(reason));
        EnsureUtc(placedAt, nameof(placedAt));
        return new CoachingLegalHold
        {
            SubjectUserId = subjectUserId,
            Reason = reason.Trim(),
            PlacedByUserId = placedByUserId,
            PlacedAt = placedAt,
            CreatedAt = placedAt
        };
    }

    public bool IsActiveAt(DateTime timestamp) =>
        PlacedAt <= timestamp && (!ReleasedAt.HasValue || ReleasedAt.Value > timestamp);

    public void Release(Guid releasedByUserId, DateTime releasedAt)
    {
        if (releasedByUserId == Guid.Empty)
            throw new ArgumentException("Operator is required.", nameof(releasedByUserId));
        EnsureUtc(releasedAt, nameof(releasedAt));
        if (releasedAt < PlacedAt)
            throw new ArgumentOutOfRangeException(nameof(releasedAt));
        if (ReleasedAt.HasValue)
            throw new InvalidOperationException("Legal hold has already been released.");
        ReleasedByUserId = releasedByUserId;
        ReleasedAt = releasedAt;
        UpdatedAt = releasedAt;
    }

    private static void EnsureUtc(DateTime timestamp, string parameterName)
    {
        if (timestamp.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}

public sealed class CoachingErasureAssessment : AggregateRoot
{
    public Guid RequestId { get; private set; }
    public Guid SubjectUserId { get; private set; }
    public bool DryRun { get; private set; }
    public bool CanProceed { get; private set; }
    public bool HasActiveLegalHold { get; private set; }
    public int AssignmentCount { get; private set; }
    public int AttachmentCount { get; private set; }
    public int ExamResultCount { get; private set; }
    public int GoalCount { get; private set; }
    public int SessionCount { get; private set; }
    public int AgreementCount { get; private set; }
    public int StudyPlanningRecordCount { get; private set; }
    public DateTime AssessedAt { get; private set; }

    private CoachingErasureAssessment() { }
    private CoachingErasureAssessment(Guid id) : base(id) { }

    public static CoachingErasureAssessment Create(
        Guid requestId,
        Guid subjectUserId,
        bool dryRun,
        bool hasActiveLegalHold,
        int assignmentCount,
        int attachmentCount,
        int examResultCount,
        int goalCount,
        int sessionCount,
        int agreementCount,
        DateTime assessedAt,
        int studyPlanningRecordCount = 0)
    {
        if (requestId == Guid.Empty || subjectUserId == Guid.Empty)
            throw new ArgumentException("Request and subject are required.");
        if (!dryRun)
            throw new InvalidOperationException("Physical erasure is not enabled.");
        if (assessedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Assessment timestamp must be UTC.", nameof(assessedAt));

        return new CoachingErasureAssessment(requestId)
        {
            RequestId = requestId,
            SubjectUserId = subjectUserId,
            DryRun = true,
            HasActiveLegalHold = hasActiveLegalHold,
            CanProceed = !hasActiveLegalHold,
            AssignmentCount = assignmentCount,
            AttachmentCount = attachmentCount,
            ExamResultCount = examResultCount,
            GoalCount = goalCount,
            SessionCount = sessionCount,
            AgreementCount = agreementCount,
            StudyPlanningRecordCount = studyPlanningRecordCount,
            AssessedAt = assessedAt,
            CreatedAt = assessedAt
        };
    }
}

public sealed class CoachingErasureExecution : AggregateRoot
{
    public Guid RequestId { get; private set; }
    public int DeletedRecordCount { get; private set; }
    public DateTime CompletedAt { get; private set; }

    private CoachingErasureExecution() { }
    private CoachingErasureExecution(Guid id) : base(id) { }

    public static CoachingErasureExecution Complete(
        Guid requestId,
        int deletedRecordCount,
        DateTime completedAt)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("Request is required.", nameof(requestId));
        if (deletedRecordCount < 0)
            throw new ArgumentOutOfRangeException(nameof(deletedRecordCount));
        if (completedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Completion timestamp must be UTC.", nameof(completedAt));

        return new CoachingErasureExecution(Guid.NewGuid())
        {
            RequestId = requestId,
            DeletedRecordCount = deletedRecordCount,
            CompletedAt = completedAt,
            CreatedAt = completedAt
        };
    }
}
