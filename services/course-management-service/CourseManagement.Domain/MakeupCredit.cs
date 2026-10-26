namespace CourseManagement.Domain;

public enum MakeupCreditStatus
{
    PendingTeacherApproval = 1,
    Available = 2,
    Scheduled = 3,
    Consumed = 4,
    Expired = 5
}

public enum MakeupCreditReason
{
    StaffCancellation = 1,
    StudentEarlyCancellation = 2
}

public sealed class MakeupCredit
{
    private MakeupCredit() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid SourceLessonId { get; private set; }
    public Guid? MakeupLessonId { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public MakeupCreditReason Reason { get; private set; }
    public MakeupCreditStatus Status { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public static MakeupCredit Create(Guid id, Guid workspaceId, Guid studentId, Guid sourceLessonId,
        Guid grantedByUserId, MakeupCreditReason reason, DateTimeOffset grantedAt)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || studentId == Guid.Empty
            || sourceLessonId == Guid.Empty || grantedByUserId == Guid.Empty || !Enum.IsDefined(reason))
            throw new ArgumentException("Make-up credit identifiers and reason are required.");

        var createdAt = grantedAt.ToUniversalTime();
        return new MakeupCredit
        {
            Id = id,
            WorkspaceId = workspaceId,
            StudentId = studentId,
            SourceLessonId = sourceLessonId,
            GrantedByUserId = grantedByUserId,
            Reason = reason,
            Status = reason == MakeupCreditReason.StudentEarlyCancellation
                ? MakeupCreditStatus.PendingTeacherApproval
                : MakeupCreditStatus.Available,
            GrantedAt = createdAt,
            ExpiresAt = createdAt.AddDays(30)
        };
    }

    public MakeupCreditStatus EffectiveStatus(DateTimeOffset now) =>
        (Status is MakeupCreditStatus.PendingTeacherApproval or MakeupCreditStatus.Available)
            && ExpiresAt <= now.ToUniversalTime() ? MakeupCreditStatus.Expired : Status;

    public void Approve(DateTimeOffset now)
    {
        if (ExpiresAt <= now.ToUniversalTime())
        {
            Status = MakeupCreditStatus.Expired;
            return;
        }
        if (Status != MakeupCreditStatus.PendingTeacherApproval)
            throw new InvalidOperationException("Only pending make-up credits can be approved.");
        Status = MakeupCreditStatus.Available;
    }

    public void Schedule(Guid lessonId, DateTimeOffset lessonEndsAt, DateTimeOffset now)
    {
        if (lessonId == Guid.Empty) throw new ArgumentException("Make-up lesson identifier is required.", nameof(lessonId));
        if (ExpiresAt <= now.ToUniversalTime() || lessonEndsAt.ToUniversalTime() > ExpiresAt)
        {
            Status = MakeupCreditStatus.Expired;
            return;
        }
        if (Status != MakeupCreditStatus.Available)
            throw new InvalidOperationException("Only available make-up credits can be scheduled.");
        MakeupLessonId = lessonId;
        Status = MakeupCreditStatus.Scheduled;
    }

    public void ReleaseFromCancelledLesson(DateTimeOffset now)
    {
        if (Status != MakeupCreditStatus.Scheduled)
            throw new InvalidOperationException("Only scheduled make-up credits can be released.");
        MakeupLessonId = null;
        Status = ExpiresAt <= now.ToUniversalTime() ? MakeupCreditStatus.Expired : MakeupCreditStatus.Available;
    }

    public void StudentCancelledMakeup(bool isEarly, DateTimeOffset now)
    {
        if (Status != MakeupCreditStatus.Scheduled)
            throw new InvalidOperationException("Only scheduled make-up credits can be changed.");
        MakeupLessonId = null;
        Status = !isEarly ? MakeupCreditStatus.Consumed
            : ExpiresAt <= now.ToUniversalTime() ? MakeupCreditStatus.Expired
            : MakeupCreditStatus.PendingTeacherApproval;
        if (Status == MakeupCreditStatus.PendingTeacherApproval)
            Reason = MakeupCreditReason.StudentEarlyCancellation;
    }

    public void Consume()
    {
        if (Status != MakeupCreditStatus.Scheduled)
            throw new InvalidOperationException("Only scheduled make-up credits can be consumed.");
        Status = MakeupCreditStatus.Consumed;
    }
}
