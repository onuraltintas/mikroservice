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

public interface ISpeedReadingPrivacyInventoryRepository
{
    Task<IReadOnlyDictionary<string, int>> CountByUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

public sealed class SpeedReadingErasureAssessmentService(
    ISpeedReadingPrivacyInventoryRepository inventoryRepository,
    TimeProvider timeProvider) : ISpeedReadingErasureAssessmentService
{
    public async Task<SpeedReadingErasureAssessment> AssessAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken)
    {
        if (!message.DryRun)
            throw new InvalidOperationException("Only dry-run privacy assessments are supported.");

        var counts = await inventoryRepository.CountByUserAsync(
            message.SubjectUserId,
            cancellationToken);
        var hasFinancialRetention = counts.GetValueOrDefault("financialRecords") > 0;
        return new SpeedReadingErasureAssessment(
            message.RequestId,
            message.SubjectUserId,
            CanProceed: !hasFinancialRetention,
            HasActiveLegalHold: hasFinancialRetention,
            counts,
            timeProvider.GetUtcNow().UtcDateTime);
    }
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
