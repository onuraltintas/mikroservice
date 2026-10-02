using Coaching.Application.Privacy;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Privacy;

public sealed class CoachingErasureAssessmentService(
    CoachingDbContext context,
    TimeProvider timeProvider) : ICoachingErasureAssessmentService
{
    public async Task<CoachingErasureAssessment> AssessAsync(
        PersonalDataErasureAssessmentRequestedV1 message,
        CancellationToken cancellationToken)
    {
        if (!message.DryRun)
            throw new InvalidOperationException("Physical erasure is not enabled.");

        var existing = await context.CoachingErasureAssessments
            .SingleOrDefaultAsync(assessment => assessment.RequestId == message.RequestId, cancellationToken);
        if (existing is not null)
            return existing;

        var assessedAt = timeProvider.GetUtcNow().UtcDateTime;
        var hasLegalHold = await context.CoachingLegalHolds.AnyAsync(
            hold => hold.SubjectUserId == message.SubjectUserId
                && hold.PlacedAt <= assessedAt
                && (!hold.ReleasedAt.HasValue || hold.ReleasedAt > assessedAt),
            cancellationToken);
        var assignmentCount = await context.AssignmentStudents.CountAsync(
            row => row.StudentId == message.SubjectUserId, cancellationToken);
        var attachmentCount = await context.AssignmentSubmissionAttachments.CountAsync(
            attachment => attachment.AssignmentStudent.StudentId == message.SubjectUserId,
            cancellationToken);
        var examResultCount = await context.ExamResults.CountAsync(
            row => row.StudentId == message.SubjectUserId, cancellationToken);
        var goalCount = await context.AcademicGoals.CountAsync(
            row => row.StudentId == message.SubjectUserId, cancellationToken);
        var sessionCount = await context.SessionAttendances.CountAsync(
            row => row.StudentId == message.SubjectUserId, cancellationToken);
        var agreementCount = await context.CoachingAgreementAcknowledgements.CountAsync(
            row => row.SubjectStudentId == message.SubjectUserId, cancellationToken);
        var planningCount = await context.Exams.CountAsync(x => x.StudentOwnerId == message.SubjectUserId, cancellationToken)
            + await context.StudyPlanRevisions.CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await context.StudyPlanTasks.CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await context.StudyAvailability.CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await CoachingStudyPlanErasureMessages.ForStudent(context, message.SubjectUserId).CountAsync(cancellationToken);

        var assessment = CoachingErasureAssessment.Create(
            message.RequestId,
            message.SubjectUserId,
            message.DryRun,
            hasLegalHold,
            assignmentCount,
            attachmentCount,
            examResultCount,
            goalCount,
            sessionCount,
            agreementCount,
            assessedAt, planningCount);
        context.CoachingErasureAssessments.Add(assessment);
        await context.SaveChangesAsync(cancellationToken);
        return assessment;
    }
}
