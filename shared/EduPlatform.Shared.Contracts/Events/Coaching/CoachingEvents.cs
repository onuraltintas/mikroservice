namespace EduPlatform.Shared.Contracts.Events.Coaching;

public sealed record AssignmentCreatedEvent(
    Guid AssignmentId,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    DateTime DueDate,
    Guid[] StudentIds);

public sealed record AssignmentSubmittedEvent(
    Guid AssignmentId,
    Guid StudentId,
    DateTime SubmittedAt);

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

public sealed record ExamResultAddedEvent(
    Guid ExamId,
    Guid StudentId,
    decimal Score,
    int? Ranking);

public sealed record SessionScheduledEvent(
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
