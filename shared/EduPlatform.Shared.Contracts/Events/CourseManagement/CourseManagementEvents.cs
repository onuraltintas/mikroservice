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

/// <summary>
/// A service e-mail to an institution's owner from eduivme (19.11.2026): support asked to look at the institution's
/// records, access opened, or it ended. Sent from the platform's own address; not a marketing mail, so no unsubscribe.
/// </summary>
public sealed record CourseManagementOwnerEmailRequestedV1(
    Guid EmailId,
    string RecipientEmail,
    string Subject,
    string Title,
    IReadOnlyList<string> Paragraphs,
    string LinkUrl,
    string LinkText);

/// <summary>
/// maxkampus.com registration (19.11.2026): Identity opened an account for an institution owner ("institution") or a
/// teacher who gives private lessons ("teacher"); Course Management opens the workspace under the name the person
/// gave. The workspace id is chosen by Identity so a repeated message opens nothing twice.
/// </summary>
public sealed record CourseManagementWorkspaceRequestedV1(
    Guid UserId,
    Guid WorkspaceId,
    string WorkspaceName,
    string Kind,
    string DisplayName,
    DateTimeOffset RequestedAt);
