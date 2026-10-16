using Coaching.Application.Attachments;
using Coaching.Application.Privacy;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Privacy;

public sealed class CoachingErasureExecutionService(
    CoachingDbContext context,
    IAssignmentAttachmentStorage attachmentStorage,
    TimeProvider timeProvider) : ICoachingErasureExecutionService
{
    public async Task<CoachingErasureExecution> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken)
    {
        if (message.Scope is not (PersonalDataScope.Account or PersonalDataScope.Coaching))
            throw new InvalidOperationException("The erasure scope does not include Coaching.");

        if (!context.Database.IsRelational()) return await ExecuteLockedAsync(message, cancellationToken);
        async Task<CoachingErasureExecution> RunAsync()
        {
            var key = "coaching-study-plan:" + message.SubjectUserId;
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            var financeKey = "coaching-finance:" + message.SubjectUserId;
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({financeKey}, 0))", cancellationToken);
            await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('coaching-session-scheduling', 0))", cancellationToken);
            return await ExecuteLockedAsync(message, cancellationToken);
        }
        if (context.Database.CurrentTransaction is not null) return await RunAsync();
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await RunAsync();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            finally { context.ChangeTracker.Clear(); }
        });
    }

    private async Task<CoachingErasureExecution> ExecuteLockedAsync(
        PersonalDataErasureExecutionRequestedV1 message, CancellationToken cancellationToken)
    {

        var existing = await context.CoachingErasureExecutions
            .SingleOrDefaultAsync(execution => execution.RequestId == message.RequestId, cancellationToken);
        if (existing is not null)
            return existing;

        var assessment = await context.CoachingErasureAssessments
            .SingleOrDefaultAsync(candidate => candidate.RequestId == message.RequestId, cancellationToken)
            ?? throw new InvalidOperationException("A completed Coaching erasure assessment is required.");
        if (assessment.SubjectUserId != message.SubjectUserId || !assessment.CanProceed)
            throw new InvalidOperationException("The Coaching erasure assessment does not authorize deletion.");

        var completedAt = timeProvider.GetUtcNow().UtcDateTime;
        var hasActiveLegalHold = await context.CoachingLegalHolds.AnyAsync(
            hold => hold.SubjectUserId == message.SubjectUserId
                && hold.PlacedAt <= completedAt
                && (!hold.ReleasedAt.HasValue || hold.ReleasedAt > completedAt),
            cancellationToken);
        if (hasActiveLegalHold)
            throw new InvalidOperationException("Coaching erasure is blocked by an active legal hold.");

        var attachments = await context.AssignmentSubmissionAttachments
            .Where(attachment => attachment.AssignmentStudent.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        foreach (var attachment in attachments)
            await attachmentStorage.DeleteAsync(attachment.StorageKey, cancellationToken);

        var assignmentStudents = await context.AssignmentStudents
            .Where(row => row.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var examResults = await context.ExamResults
            .Where(row => row.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var studentExams = await context.Exams.Where(x => x.StudentOwnerId == message.SubjectUserId).ToListAsync(cancellationToken);
        var studyTasks = await context.StudyPlanTasks.Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var studyPlans = await context.StudyPlanRevisions.Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var studyAvailability = await context.StudyAvailability.Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var studyMessages = await CoachingStudyPlanErasureMessages.ForStudent(context, message.SubjectUserId).ToListAsync(cancellationToken);
        var reminders = await context.CoachingReminderReceipts.Where(x => x.RecipientUserId == message.SubjectUserId).ToListAsync(cancellationToken);
        var fees = await context.CoachingFeeAgreements.Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var financeCount = fees.Count
            + await context.CoachingFeeInstallments.CountAsync(x => context.CoachingFeeAgreements.Any(fee => fee.Id == x.AgreementId && fee.StudentId == message.SubjectUserId), cancellationToken)
            + await context.CoachingFeePayments.CountAsync(x => context.CoachingFeeAgreements.Any(fee => fee.Id == x.AgreementId && fee.StudentId == message.SubjectUserId), cancellationToken);
        var goals = await context.AcademicGoals
            .Where(row => row.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var attendances = await context.SessionAttendances
            .Where(row => row.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var acknowledgements = await context.CoachingAgreementAcknowledgements
            .Where(row => row.SubjectStudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);

        var ownedAssignments = await context.CoachingTeacherStudentAssignments.Where(x => x.StudentUserId == message.SubjectUserId || x.TeacherUserId == message.SubjectUserId).ToListAsync(cancellationToken);
        var ownedMembers = await context.CoachingInstitutionMemberships.Where(x => x.UserId == message.SubjectUserId).ToListAsync(cancellationToken);
        var ownedStudents = await context.CoachingStudentProfiles.Where(x => x.UserId == message.SubjectUserId).ToListAsync(cancellationToken);
        var learningContexts = await context.StudentLearningContexts.Include(x => x.CoursePreferences).Include(x => x.TopicStates)
            .Where(x => x.StudentUserId == message.SubjectUserId).ToListAsync(cancellationToken);
        var ownedTeachers = await context.CoachingTeacherProfiles.Where(x => x.UserId == message.SubjectUserId).ToListAsync(cancellationToken);
        var ownedInvites = await context.CoachingInvitations.Where(x => x.InviterUserId == message.SubjectUserId || x.TeacherUserId == message.SubjectUserId || x.AcceptedByUserId == message.SubjectUserId).ToListAsync(cancellationToken);

        var deletedRecordCount = learningContexts.Sum(x => 1 + x.CoursePreferences.Count + x.TopicStates.Count) + ownedAssignments.Count + ownedMembers.Count + ownedStudents.Count + ownedTeachers.Count + ownedInvites.Count + attachments.Count
            + assignmentStudents.Count
            + examResults.Count
            + studentExams.Count + studyTasks.Count + studyPlans.Count + studyAvailability.Count + studyMessages.Count + reminders.Count + financeCount
            + goals.Count
            + attendances.Count
            + acknowledgements.Count;
        var execution = CoachingErasureExecution.Complete(
            message.RequestId, deletedRecordCount, completedAt);

        context.CoachingTeacherStudentAssignments.RemoveRange(ownedAssignments);
        context.CoachingInstitutionMemberships.RemoveRange(ownedMembers);
        context.StudentLearningContexts.RemoveRange(learningContexts);
        context.CoachingStudentProfiles.RemoveRange(ownedStudents);
        context.CoachingTeacherProfiles.RemoveRange(ownedTeachers);
        context.CoachingInvitations.RemoveRange(ownedInvites);
        context.AssignmentSubmissionAttachments.RemoveRange(attachments);
        context.AssignmentStudents.RemoveRange(assignmentStudents);
        context.ExamResults.RemoveRange(examResults);
        context.Exams.RemoveRange(studentExams);
        context.StudyPlanTasks.RemoveRange(studyTasks);
        context.StudyPlanRevisions.RemoveRange(studyPlans);
        context.StudyAvailability.RemoveRange(studyAvailability);
        context.RemoveRange(studyMessages);
        context.CoachingReminderReceipts.RemoveRange(reminders);
        context.CoachingFeeAgreements.RemoveRange(fees);
        context.AcademicGoals.RemoveRange(goals);
        context.SessionAttendances.RemoveRange(attendances);
        context.CoachingAgreementAcknowledgements.RemoveRange(acknowledgements);
        context.CoachingErasureAssessments.Remove(assessment);
        context.CoachingErasureExecutions.Add(execution);
        await context.SaveChangesAsync(cancellationToken);
        return execution;
    }
}
