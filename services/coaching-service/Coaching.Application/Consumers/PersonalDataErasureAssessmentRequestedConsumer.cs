using Coaching.Application.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;

namespace Coaching.Application.Consumers;

public sealed class PersonalDataErasureAssessmentRequestedConsumer(
    ICoachingErasureAssessmentService assessmentService,
    IPublishEndpoint publishEndpoint)
    : IConsumer<PersonalDataErasureAssessmentRequestedV1>
{
    public async Task Consume(ConsumeContext<PersonalDataErasureAssessmentRequestedV1> context)
    {
        if (context.Message.Scope is not (PersonalDataScope.Account or PersonalDataScope.Coaching))
            return;

        var assessment = await assessmentService.AssessAsync(
            context.Message,
            context.CancellationToken);
        await publishEndpoint.Publish(
            new PersonalDataErasureAssessmentCompletedV1(
                assessment.Id,
                assessment.RequestId,
                assessment.SubjectUserId,
                "Coaching",
                assessment.CanProceed,
                assessment.HasActiveLegalHold,
                assessment.AssignmentCount,
                assessment.AttachmentCount,
                assessment.ExamResultCount,
                assessment.GoalCount,
                assessment.SessionCount,
                assessment.AgreementCount,
                assessment.AssessedAt),
            publishContext =>
            {
                publishContext.MessageId = assessment.Id;
                publishContext.CorrelationId = assessment.RequestId;
            },
            context.CancellationToken);
    }
}
