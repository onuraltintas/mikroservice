using EduPlatform.Shared.Contracts.Events.Privacy;
using Identity.Application.DataSubjectRequests;
using MassTransit;

namespace Identity.Infrastructure.Messaging;

public sealed class DataSubjectRequestEventPublisher(IPublishEndpoint publishEndpoint)
    : IDataSubjectRequestEventPublisher
{
    public Task PublishAssessmentRequestedAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken) =>
        publishEndpoint.Publish(message, context =>
        {
            context.MessageId = message.EventId;
            context.CorrelationId = message.RequestId;
        }, cancellationToken);
}
