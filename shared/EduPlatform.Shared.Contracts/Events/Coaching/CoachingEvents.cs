namespace EduPlatform.Shared.Contracts.Events.Coaching;

public sealed record StudyPlanPublishedEvent(Guid RevisionId, Guid StudentId);
public sealed record StudyPlanUpdatedEvent(Guid RevisionId, Guid StudentId, string ChangeDescription);
public sealed record WeeklyReflectionSavedEvent(Guid ReflectionId, Guid StudentId, DateOnly WeekStart, Guid[] TeacherIds);
public sealed record WeeklyDecisionChangedEvent(Guid DecisionId, Guid StudentId, Guid TeacherId, DateOnly WeekStart, bool Created, bool ResponsibilityTransferred = false);
public sealed record CoachingReminderDueEvent(Guid ReminderId, Guid RecipientUserId, Guid SourceId, string Kind, DateTime DueAt);

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
    Guid? TeacherId = null);

public sealed record AssignmentGradedEvent(
    Guid AssignmentId,
    Guid StudentId,
    decimal Score,
    string? TeacherFeedback,
    DateTime GradedAt);

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
}
