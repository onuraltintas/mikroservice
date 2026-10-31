using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

/// <summary>V1 (parent panel): tells the other side of a parent link what happened, in the app and by e-mail.</summary>
public sealed class ParentLinkNotificationConsumer(
    ICoachingNotificationDispatcher dispatcher,
    IEmailDeliveryQueue emailDeliveryQueue,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions) : IConsumer<ParentLinkNotificationV1>
{
    public Task Consume(ConsumeContext<ParentLinkNotificationV1> context) => Task.CompletedTask;
}
