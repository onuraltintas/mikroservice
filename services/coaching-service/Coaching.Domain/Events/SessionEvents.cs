using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Events;

/// <summary>
/// Koçluk seansı planlandı event'i
/// </summary>
public record SessionScheduledEvent(
    Guid SessionId,
    Guid TeacherId,
    List<Guid> StudentIds,
    DateTime ScheduledDate
) : DomainEvent
{
    public override string EventType => nameof(SessionScheduledEvent);
}

/// <summary>
/// Koçluk seansı tamamlandı event'i
/// </summary>
public record SessionCompletedEvent(
    Guid SessionId,
    Guid TeacherId,
    List<Guid> AttendedStudentIds,
    DateTime CompletedAt
) : DomainEvent
{
    public override string EventType => nameof(SessionCompletedEvent);
}
