namespace EduPlatform.Shared.Kernel.Primitives;

/// <summary>
/// Marker interface for domain events
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
}

/// <summary>
/// Base class for domain events
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public abstract string EventType { get; }
}
