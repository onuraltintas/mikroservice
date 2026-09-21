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
