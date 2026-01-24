using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Events;

/// <summary>
/// Ödev oluşturuldu event'i
/// </summary>
public record AssignmentCreatedEvent(
    Guid AssignmentId,
    Guid TeacherId,
    string Title,
    DateTime DueDate,
    List<Guid> StudentIds
) : DomainEvent
{
    public override string EventType => nameof(AssignmentCreatedEvent);
}

/// <summary>
/// Ödev teslim edildi event'i  
/// </summary>
public record AssignmentSubmittedEvent(
    Guid AssignmentId,
    Guid StudentId,
    DateTime SubmittedAt
) : DomainEvent
{
    public override string EventType => nameof(AssignmentSubmittedEvent);
}

/// <summary>
/// Ödev notlandırıldı event'i
/// </summary>
public record AssignmentGradedEvent(
    Guid AssignmentId,
    Guid StudentId,
    decimal Score,
    string? TeacherFeedback
) : DomainEvent
{
    public override string EventType => nameof(AssignmentGradedEvent);
}
