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
        var goals = await context.AcademicGoals
            .Where(row => row.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var attendances = await context.SessionAttendances
            .Where(row => row.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var acknowledgements = await context.CoachingAgreementAcknowledgements
            .Where(row => row.SubjectStudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);

        var deletedRecordCount = attachments.Count
            + assignmentStudents.Count
            + examResults.Count
            + studentExams.Count + studyTasks.Count + studyPlans.Count + studyAvailability.Count + studyMessages.Count
            + goals.Count
            + attendances.Count
            + acknowledgements.Count;
        var execution = CoachingErasureExecution.Complete(
            message.RequestId, deletedRecordCount, completedAt);

        context.AssignmentSubmissionAttachments.RemoveRange(attachments);
        context.AssignmentStudents.RemoveRange(assignmentStudents);
        context.ExamResults.RemoveRange(examResults);
        context.Exams.RemoveRange(studentExams);
        context.StudyPlanTasks.RemoveRange(studyTasks);
        context.StudyPlanRevisions.RemoveRange(studyPlans);
        context.StudyAvailability.RemoveRange(studyAvailability);
        context.RemoveRange(studyMessages);
        context.AcademicGoals.RemoveRange(goals);
        context.SessionAttendances.RemoveRange(attendances);
        context.CoachingAgreementAcknowledgements.RemoveRange(acknowledgements);
        context.CoachingErasureAssessments.Remove(assessment);
        context.CoachingErasureExecutions.Add(execution);
        await context.SaveChangesAsync(cancellationToken);
        return execution;
    }
}
