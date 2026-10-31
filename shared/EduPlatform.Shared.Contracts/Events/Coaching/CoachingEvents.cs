namespace EduPlatform.Shared.Contracts.Events.Coaching;

public sealed record StudyPlanPublishedEvent(Guid RevisionId, Guid StudentId);
public sealed record StudyPlanUpdatedEvent(Guid RevisionId, Guid StudentId, string ChangeDescription);
public sealed record WeeklyReflectionSavedEvent(Guid ReflectionId, Guid StudentId, DateOnly WeekStart, Guid[] TeacherIds);
public sealed record WeeklyDecisionChangedEvent(Guid DecisionId, Guid StudentId, Guid TeacherId, DateOnly WeekStart, bool Created, bool ResponsibilityTransferred = false);
public sealed record CoachingReminderDueEvent(Guid ReminderId, Guid RecipientUserId, Guid SourceId, string Kind, DateTime DueAt)
{
    /// <summary>Roadmap O8: the assignment title of an "AssignmentDue" reminder; empty for other kinds.</summary>
    public string? SubjectTitle { get; init; }
}

public sealed record AssignmentCreatedEvent(
    Guid AssignmentId,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    DateTime DueDate,
    Guid[] StudentIds);

public sealed record AssignmentUpdatedEvent(
    Guid AssignmentId,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    DateTime DueDate,
    Guid[] StudentIds);

public sealed record AssignmentSubmittedEvent(
    Guid AssignmentId,
    Guid StudentId,
    DateTime SubmittedAt,
    Guid? TeacherId = null)
{
    /// <summary>Roadmap O8: lets the teacher's notification name the assignment.</summary>
    public string? AssignmentTitle { get; init; }
}

public sealed record AssignmentGradedEvent(
    Guid AssignmentId,
    Guid StudentId,
    decimal Score,
    string? TeacherFeedback,
    DateTime GradedAt)
{
    /// <summary>Roadmap O8: lets the student's notification name the assignment.</summary>
    public string? AssignmentTitle { get; init; }
}

public sealed record ExamCreatedEvent(
    Guid ExamId,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    DateTime ExamDate);

public sealed record ExamUpdatedEvent(
    Guid ExamId,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    DateTime ExamDate,
    decimal MaxScore,
    Guid[] StudentIds);

public sealed record ExamResultAddedEvent(
    Guid ExamId,
    Guid StudentId,
    decimal Score,
    int? Ranking);

public sealed record ExamResultUpdatedEvent(
    Guid ExamId,
    Guid ResultId,
    Guid StudentId,
    decimal Score,
    int? Ranking);

public sealed record SessionScheduledEvent(
    Guid SessionId,
    Guid TeacherId,
    Guid? InstitutionId,
    Guid[] StudentIds,
    DateTime ScheduledDate);

public sealed record SessionUpdatedEvent(
    Guid SessionId,
    Guid TeacherId,
    Guid? InstitutionId,
    Guid[] StudentIds,
    DateTime ScheduledDate);

public sealed record SessionCancelledEvent(
    Guid SessionId,
    Guid TeacherId,
    Guid? InstitutionId,
    Guid[] StudentIds,
    DateTime ScheduledDate);

public sealed record GoalCreatedEvent(
    Guid GoalId,
    Guid StudentId,
    Guid? SetByTeacherId,
    string Title);

public sealed record GoalUpdatedEvent(
    Guid GoalId,
    Guid StudentId,
    Guid? SetByTeacherId,
    string Title);

/// <summary>
/// A subscription or bank-transfer event the payer (or the platform admin) is told about in-app and by e-mail.
/// <see cref="RecipientUserId"/> is null for platform-admin notices (<see cref="CoachingSubscriptionNoticeKinds.TransferSubmitted"/>)
/// and for institution notices, whose contact address (if any) is <see cref="RecipientEmail"/>.
/// </summary>
public sealed record CoachingSubscriptionNoticeV1(
    Guid NoticeId,
    Guid? RecipientUserId,
    string? RecipientEmail,
    string Kind,
    string Audience,
    string PlanName,
    DateTime? EndsAt,
    string? Detail,
    DateTime OccurredAt);

public static class CoachingSubscriptionNoticeKinds
{
    public const string TransferSubmitted = "TransferSubmitted";
    public const string TransferApproved = "TransferApproved";
    public const string TransferRejected = "TransferRejected";
    public const string ApprovalCancelled = "ApprovalCancelled";
    public const string Expiring = "Expiring";
    public const string Expired = "Expired";
    public const string InstitutionExpiring = "InstitutionExpiring";
    public const string InstitutionExpired = "InstitutionExpired";
    /// <summary>A pending transfer nobody could verify in time; the payer can send a new notice.</summary>
    public const string TransferExpired = "TransferExpired";
}

/// <summary>An assignment was called off (cancelled, or deleted before anyone submitted); its students are told.</summary>
public sealed record AssignmentCancelledEvent(
    Guid AssignmentId,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    Guid[] StudentIds);

/// <summary>A coach's link with a student ended without the coach's own action; the coach is told.</summary>
public sealed record CoachingTeacherLinkEndedEvent(Guid TeacherUserId, Guid StudentUserId, string StudentName, string Reason)
{
    /// <summary>Roadmap O12: set when the teacher ended the link, so the student can be told who.</summary>
    public string? TeacherName { get; init; }
    /// <summary>K2: the institution whose link ended (null for an independent coach).</summary>
    public Guid? InstitutionId { get; init; }
    /// <summary>K2: the institution administrator who ended the link; told what was closed.</summary>
    public Guid? InstitutionActorUserId { get; init; }
    /// <summary>K2: open assignments of this teacher withdrawn from the student.</summary>
    public int CancelledAssignments { get; init; }
    /// <summary>K2: coming sessions of this teacher the student left (cancelled when nobody else remained).</summary>
    public int RemovedSessions { get; init; }
}

public static class CoachingTeacherLinkEndedReasons
{
    /// <summary>The student joined an institution; an independent coach link cannot continue alongside it.</summary>
    public const string JoinedInstitution = "JoinedInstitution";
    /// <summary>The student ended the link themselves.</summary>
    public const string StudentLeft = "StudentLeft";
    /// <summary>Roadmap O12: an independent teacher removed the student.</summary>
    public const string TeacherRemoved = "TeacherRemoved";
    /// <summary>K2: the institution ended the link (a link ended, the teacher or the student left the institution).</summary>
    public const string InstitutionEnded = "InstitutionEnded";
}

/// <summary>Roadmap Y7: one week of a child's Coaching activity, e-mailed to a linked parent on Sunday evening.</summary>
public sealed record CoachingParentWeeklySummaryV1(
    Guid SummaryId,
    Guid ParentUserId,
    string ParentEmail,
    string ParentName,
    Guid StudentUserId,
    string StudentName,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int StudyTasksPlanned,
    int StudyTasksCompleted,
    int AssignmentsDue,
    int AssignmentsSubmitted,
    int AssignmentsOverdue,
    int SessionsHeld,
    int SessionsAttended,
    int SessionsUpcoming,
    string? LatestExamTitle,
    decimal? LatestExamScore);

/// <summary>
/// Roadmap O3: a conversation has a new message for the recipient. Sent once per unread batch and without the text,
/// so the notification never carries the message itself. RecipientRole is Teacher, Student or Parent.
/// </summary>
public sealed record CoachingMessageReceivedEvent(Guid ConversationId, Guid RecipientUserId, string RecipientRole, string SenderName,
    bool HasAttachments);
