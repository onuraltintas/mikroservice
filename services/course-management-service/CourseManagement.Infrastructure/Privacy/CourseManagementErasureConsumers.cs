using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;

namespace CourseManagement.Infrastructure.Privacy;

public sealed class CourseManagementErasureAssessmentRequestedConsumer(
    ICourseManagementAccountErasureService assessmentService,
    IPublishEndpoint publishEndpoint)
    : IConsumer<PersonalDataErasureAssessmentRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureAssessmentRequestedV1> context)
    {
        if (context.Message.Scope != PersonalDataScope.Account) return;
        if (!context.Message.DryRun)
            throw new InvalidOperationException("Only dry-run Course Management erasure assessments are supported.");

        var assessment = await assessmentService.AssessAsync(context.Message, context.CancellationToken);
        var completion = new PersonalDataErasureAssessmentCompletedV1(
            Guid.NewGuid(), assessment.RequestId, assessment.SubjectUserId, "CourseManagement",
            assessment.CanProceed, assessment.HasActiveLegalHold, assessment.AssignmentCount,
            assessment.AttachmentCount, 0, 0, assessment.SessionCount, assessment.AgreementCount,
            assessment.AssessedAt.UtcDateTime, assessment.RecordCounts);
        await publishEndpoint.Publish(completion,
            publishContext =>
            {
                publishContext.MessageId = completion.EventId;
                publishContext.CorrelationId = assessment.RequestId;
            }, context.CancellationToken);
    }
}

public sealed class CourseManagementErasureExecutionRequestedConsumer(
    ICourseManagementAccountErasureService executionService,
    IPublishEndpoint publishEndpoint)
    : IConsumer<PersonalDataErasureExecutionRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureExecutionRequestedV1> context)
    {
        if (context.Message.Scope != PersonalDataScope.Account) return;

        var execution = await executionService.ExecuteAsync(context.Message, context.CancellationToken);
        var completion = new PersonalDataErasureExecutionCompletedV1(
            Guid.NewGuid(), execution.RequestId, "CourseManagement", execution.DeletedRecordCount,
            execution.CompletedAt.UtcDateTime);
        await publishEndpoint.Publish(completion,
            publishContext =>
            {
                publishContext.MessageId = completion.EventId;
                publishContext.CorrelationId = execution.RequestId;
            }, context.CancellationToken);
    }
}
