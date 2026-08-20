using Coaching.Application.Interfaces;
using MassTransit;

namespace Coaching.Infrastructure.Messaging;

public sealed class MassTransitCoachingEventPublisher(IPublishEndpoint publishEndpoint)
    : ICoachingEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class => publishEndpoint.Publish(message, cancellationToken);
}
