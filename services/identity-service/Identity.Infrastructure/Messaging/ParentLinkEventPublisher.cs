using EduPlatform.Shared.Contracts.Events.Identity;
using Identity.Application.ParentLinks;
using MassTransit;

namespace Identity.Infrastructure.Messaging;

/// <summary>V1: link notices go through Identity's EF bus outbox and are saved with the link change.</summary>
public sealed class ParentLinkEventPublisher(IPublishEndpoint publishEndpoint) : IParentLinkEventPublisher
{
    public Task PublishAsync(ParentLinkNotificationV1 message, CancellationToken ct) =>
        publishEndpoint.Publish(message, context =>
        {
            context.MessageId = message.EventId;
            context.CorrelationId = message.LinkId;
        }, ct);
}
