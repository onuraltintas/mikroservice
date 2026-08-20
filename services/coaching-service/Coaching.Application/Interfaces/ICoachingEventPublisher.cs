namespace Coaching.Application.Interfaces;

/// <summary>
/// Publishes Coaching integration events through the service-owned outbox.
/// </summary>
public interface ICoachingEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class;
}
