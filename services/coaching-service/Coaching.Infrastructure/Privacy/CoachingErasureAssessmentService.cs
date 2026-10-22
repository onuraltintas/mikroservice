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
        var learningIds = context.StudentLearningContexts.Where(x => x.StudentUserId == message.SubjectUserId).Select(x => x.Id);
        var planningCount = await context.Set<Coaching.Domain.Entities.StudentPrimaryGoal>().CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await context.StudentLearningContexts.CountAsync(x => x.StudentUserId == message.SubjectUserId, cancellationToken)
            + await context.StudentCoursePreferences.CountAsync(x => learningIds.Contains(x.ContextId), cancellationToken)
            + await context.StudentTopicStates.CountAsync(x => learningIds.Contains(x.ContextId), cancellationToken)
            + await context.CoachingStudentProfiles.CountAsync(x => x.UserId == message.SubjectUserId, cancellationToken)
            + await context.CoachingTeacherProfiles.CountAsync(x => x.UserId == message.SubjectUserId, cancellationToken)
            + await context.CoachingInstitutionMemberships.CountAsync(x => x.UserId == message.SubjectUserId, cancellationToken)
            + await context.CoachingTeacherStudentAssignments.CountAsync(x => x.StudentUserId == message.SubjectUserId || x.TeacherUserId == message.SubjectUserId, cancellationToken)
            + await context.CoachingInvitations.CountAsync(x => x.InviterUserId == message.SubjectUserId || x.TeacherUserId == message.SubjectUserId || x.AcceptedByUserId == message.SubjectUserId, cancellationToken)
            + await context.Exams.CountAsync(x => x.StudentOwnerId == message.SubjectUserId, cancellationToken)
            + await context.StudyPlanRevisions.CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await context.StudyPlanTasks.CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await context.StudyAvailability.CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await context.Set<WeeklyStudyDraft>().CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await context.Set<WeeklyReflection>().CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await context.Set<WeeklyDecision>().CountAsync(x => x.StudentId == message.SubjectUserId || x.TeacherId == message.SubjectUserId, cancellationToken)
            + await CoachingStudyPlanErasureMessages.DecisionWorkRequests(context, message.SubjectUserId).CountAsync(cancellationToken)
            + await CoachingStudyPlanErasureMessages.ForStudent(context, message.SubjectUserId).CountAsync(cancellationToken)
            + await context.CoachingReminderReceipts.CountAsync(x => x.RecipientUserId == message.SubjectUserId, cancellationToken);

        var financeCount = await context.CoachingFeeAgreements.CountAsync(x => x.StudentId == message.SubjectUserId, cancellationToken)
            + await context.CoachingFeeInstallments.CountAsync(x => context.CoachingFeeAgreements.Any(fee => fee.Id == x.AgreementId && fee.StudentId == message.SubjectUserId), cancellationToken)
            + await context.CoachingFeePayments.CountAsync(x => context.CoachingFeeAgreements.Any(fee => fee.Id == x.AgreementId && fee.StudentId == message.SubjectUserId), cancellationToken);
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
            assessedAt, planningCount, financeCount);
        context.CoachingErasureAssessments.Add(assessment);
        await context.SaveChangesAsync(cancellationToken);
        return assessment;
    }
}
