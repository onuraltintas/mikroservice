using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;

namespace SpeedReading.Application.Privacy;

public sealed record SpeedReadingErasureAssessment(
    Guid RequestId,
    Guid SubjectUserId,
    bool CanProceed,
    bool HasActiveLegalHold,
    IReadOnlyDictionary<string, int> RecordCounts,
    DateTime AssessedAt);

public interface ISpeedReadingErasureAssessmentService
{
    Task<SpeedReadingErasureAssessment> AssessAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken);
}

public sealed class SpeedReadingErasureAssessmentRequestedConsumer(
    ISpeedReadingErasureAssessmentService assessmentService,
    IPublishEndpoint publishEndpoint)
    : IConsumer<PersonalDataErasureAssessmentRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureAssessmentRequestedV1> context)
    {
        if (context.Message.Scope is not (PersonalDataScope.Account or PersonalDataScope.SpeedReading))
            return;

        var assessment = await assessmentService.AssessAsync(
            context.Message,
            context.CancellationToken);
        await publishEndpoint.Publish(
            new PersonalDataErasureAssessmentCompletedV1(
                Guid.NewGuid(),
                assessment.RequestId,
                assessment.SubjectUserId,
                "SpeedReading",
                assessment.CanProceed,
                assessment.HasActiveLegalHold,
                0, 0, 0, 0, 0, 0,
                assessment.AssessedAt,
                assessment.RecordCounts),
            context.CancellationToken);
    }
}
