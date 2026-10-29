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

public interface ISpeedReadingErasureExecutionService
{
    Task<SpeedReadingErasureExecutionResult> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken);
}

public sealed record SpeedReadingErasureExecutionResult(
    Guid Id,
    Guid RequestId,
    int DeletedRecordCount,
    DateTime CompletedAt);

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
        // Review H2b (decision 28.10.2026, same as coaching): paid records do not block erasure. They are kept
        // unchanged for a year and then anonymised; the assessment reports how many will be kept.
        var reported = new Dictionary<string, int>(counts)
        {
            ["RetainedFinancialRecords"] = counts.GetValueOrDefault("financialRecords")
        };
        return new SpeedReadingErasureAssessment(
            message.RequestId,
            message.SubjectUserId,
            CanProceed: true,
            HasActiveLegalHold: false,
            reported,
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

public sealed class SpeedReadingErasureExecutionRequestedConsumer(
    ISpeedReadingErasureExecutionService executionService,
    IPublishEndpoint publishEndpoint)
    : IConsumer<PersonalDataErasureExecutionRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureExecutionRequestedV1> context)
    {
        if (context.Message.Scope is not (PersonalDataScope.Account or PersonalDataScope.SpeedReading))
            return;

        var execution = await executionService.ExecuteAsync(
            context.Message, context.CancellationToken);
        await publishEndpoint.Publish(
            new PersonalDataErasureExecutionCompletedV1(
                execution.Id,
                execution.RequestId,
                "SpeedReading",
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
