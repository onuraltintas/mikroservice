using Coaching.Application.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;

namespace Coaching.Application.Consumers;

public sealed class PersonalDataErasureExecutionRequestedConsumer(
    ICoachingErasureExecutionService executionService,
    IPublishEndpoint publishEndpoint)
    : IConsumer<PersonalDataErasureExecutionRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureExecutionRequestedV1> context)
    {
        if (context.Message.Scope is not (PersonalDataScope.Account or PersonalDataScope.Coaching))
            return;

        var execution = await executionService.ExecuteAsync(
            context.Message,
            context.CancellationToken);
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
