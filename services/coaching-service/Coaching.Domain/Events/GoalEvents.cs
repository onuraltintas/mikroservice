using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Events;

/// <summary>
/// Hedef tamamlandı event'i
/// </summary>
public record GoalCompletedEvent(
    Guid GoalId,
    Guid StudentId,
    string Title,
    DateTime CompletedAt
) : DomainEvent
{
    public override string EventType => nameof(GoalCompletedEvent);
}

/// <summary>
/// Hedef oluşturuldu event'i
/// </summary>
public record GoalCreatedEvent(
    Guid GoalId,
    Guid StudentId,
    Guid? SetByTeacherId,
    string Title
) : DomainEvent
{
    public override string EventType => nameof(GoalCreatedEvent);
}
