using Coaching.Application.Privacy;
using Coaching.Domain.Entities;
using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;

namespace Coaching.Application.Consumers;

public sealed class PersonalDataErasureExecutionRequestedConsumer(
    ICoachingErasureExecutionService executionService,
    IPublishEndpoint publishEndpoint,
    TimeProvider timeProvider)
    : IConsumer<PersonalDataErasureExecutionRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureExecutionRequestedV1> context)
    {
        if (context.Message.Scope is not (PersonalDataScope.Account or PersonalDataScope.Coaching))
            return;

        CoachingErasureExecution execution;
        try
        {
            execution = await executionService.ExecuteAsync(context.Message, context.CancellationToken);
        }
        catch (CoachingErasureBlockedException blocked)
        {
            // Nothing was deleted. Tell Identity right away instead of retrying into the error queue.
            var eventId = NewId.NextGuid();
            await publishEndpoint.Publish(
                new PersonalDataErasureExecutionBlockedV1(
                    eventId, context.Message.RequestId, "Coaching", blocked.ReasonCode, timeProvider.GetUtcNow().UtcDateTime),
                publishContext =>
                {
                    publishContext.MessageId = eventId;
                    publishContext.CorrelationId = context.Message.RequestId;
                },
                context.CancellationToken);
            return;
        }
        await publishEndpoint.Publish(
            new PersonalDataErasureExecutionCompletedV1(
                execution.Id,
                execution.RequestId,
                "Coaching",
                execution.DeletedRecordCount,
                execution.CompletedAt),
            publishContext =>
            {
                publishContext.MessageId = execution.Id;
                publishContext.CorrelationId = execution.RequestId;
            },
            context.CancellationToken);
    }
}
