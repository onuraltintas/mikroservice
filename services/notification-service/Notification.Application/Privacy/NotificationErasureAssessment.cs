using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;

namespace Notification.Application.Privacy;

public sealed record NotificationErasureAssessment(
    Guid RequestId,
    Guid SubjectUserId,
    bool CanProceed,
    IReadOnlyDictionary<string, int> RecordCounts,
    DateTime AssessedAt);

public interface INotificationErasureAssessmentService
{
    Task<NotificationErasureAssessment> AssessAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken);
}

public sealed record NotificationErasureExecutionResult(
    Guid Id,
    Guid RequestId,
    int DeletedRecordCount,
    DateTime CompletedAt);

public interface INotificationErasureExecutionService
{
    Task<NotificationErasureExecutionResult> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken);
}

public sealed class NotificationErasureAssessmentRequestedConsumer(
    INotificationErasureAssessmentService assessmentService,
    IPublishEndpoint publishEndpoint)
    : IConsumer<PersonalDataErasureAssessmentRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureAssessmentRequestedV1> context)
    {
        if (context.Message.Scope != PersonalDataScope.Account)
            return;
        if (!context.Message.DryRun)
            throw new InvalidOperationException("Only dry-run privacy assessments are supported.");

        var assessment = await assessmentService.AssessAsync(context.Message, context.CancellationToken);
        await publishEndpoint.Publish(
            new PersonalDataErasureAssessmentCompletedV1(
                Guid.NewGuid(), assessment.RequestId, assessment.SubjectUserId,
                "Notification", assessment.CanProceed, HasActiveLegalHold: false,
                0, 0, 0, 0, 0, 0, assessment.AssessedAt, assessment.RecordCounts),
            context.CancellationToken);
    }
}

public sealed class NotificationErasureExecutionRequestedConsumer(
    INotificationErasureExecutionService executionService,
    IPublishEndpoint publishEndpoint)
    : IConsumer<PersonalDataErasureExecutionRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureExecutionRequestedV1> context)
    {
        if (context.Message.Scope != PersonalDataScope.Account)
            return;

        var execution = await executionService.ExecuteAsync(
            context.Message, context.CancellationToken);
        await publishEndpoint.Publish(
            new PersonalDataErasureExecutionCompletedV1(
                execution.Id,
                execution.RequestId,
                "Notification",
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
