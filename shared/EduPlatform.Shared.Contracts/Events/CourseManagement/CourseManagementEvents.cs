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
