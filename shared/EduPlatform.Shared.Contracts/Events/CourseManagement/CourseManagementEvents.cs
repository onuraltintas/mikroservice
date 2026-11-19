namespace EduPlatform.Shared.Contracts.Events.CourseManagement;

public sealed record CourseManagementLessonScheduledV1(
    Guid LessonId,
    Guid WorkspaceId,
    IReadOnlyCollection<Guid> StudentUserIds,
    DateTimeOffset StartsAt);

public sealed record CourseManagementLessonRescheduledV1(
    Guid LessonId,
    Guid WorkspaceId,
    IReadOnlyCollection<Guid> StudentUserIds,
    DateTimeOffset StartsAt);

public sealed record CourseManagementLessonCancelledV1(
    Guid LessonId,
    Guid WorkspaceId,
    IReadOnlyCollection<Guid> StudentUserIds,
    DateTimeOffset StartsAt);

public sealed record CourseManagementAssignmentPublishedV1(
    Guid AssignmentId,
    Guid WorkspaceId,
    string Title,
    DateTimeOffset DueAt,
    IReadOnlyCollection<Guid> StudentUserIds);

public sealed record CourseManagementAssignmentSubmittedV1(
    Guid AssignmentId,
    Guid WorkspaceId,
    Guid TeacherUserId);

public sealed record CourseManagementAssignmentReviewedV1(
    Guid AssignmentId,
    Guid WorkspaceId,
    Guid StudentUserId);

public sealed record CourseManagementPaymentRecordedV1(
    Guid PaymentId,
    Guid WorkspaceId,
    Guid StudentUserId);

public sealed record CourseManagementReminderDueV1(
    Guid ReminderId,
    Guid WorkspaceId,
    Guid RecipientUserId,
    string ReminderType,
    DateTimeOffset TargetAt);

/// <summary>
/// An institution's e-mail to a student or guardian (19.11.2026). Course Management decides whether, whom and what;
/// the notification service only lays it out and sends it. Paragraphs are plain text (escaped when rendered).
/// The sender shows the institution's name; replies go to the institution's address when it gave one.
/// </summary>
public sealed record CourseManagementEmailRequestedV1(
    Guid EmailId,
    Guid WorkspaceId,
    string InstitutionName,
    string? ReplyTo,
    string RecipientEmail,
    string Subject,
    string Title,
    IReadOnlyList<string> Paragraphs,
    string? InstitutionNote,
    string UnsubscribeUrl);
