using Coaching.Application.Attachments;
using Coaching.Application.Interfaces;
using Coaching.Application.Privacy;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Privacy;

public sealed class CoachingErasureExecutionService(
    CoachingDbContext context,
    IAssignmentAttachmentStorage attachmentStorage,
    TimeProvider timeProvider,
    ICoachingEventPublisher? events = null) : ICoachingErasureExecutionService
{
    // Without the outbox publisher (tests, tools) the files are deleted right after this service's own commit.
    private readonly List<string> filesToDeleteAfterCommit = [];

    public async Task<CoachingErasureExecution> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken)
    {
        if (message.Scope is not (PersonalDataScope.Account or PersonalDataScope.Coaching))
            throw new InvalidOperationException("The erasure scope does not include Coaching.");

        if (!context.Database.IsRelational())
        {
            var inMemory = await ExecuteLockedAsync(message, cancellationToken);
            await DeleteFilesAfterCommitAsync(cancellationToken);
            return inMemory;
        }
        async Task<CoachingErasureExecution> RunAsync()
        {
            var key = "coaching-study-plan:" + message.SubjectUserId;
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            var financeKey = "coaching-finance:" + message.SubjectUserId;
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({financeKey}, 0))", cancellationToken);
            await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('coaching-session-scheduling', 0))", cancellationToken);
            return await ExecuteLockedAsync(message, cancellationToken);
        }
        // Inside a caller's transaction (the consumer outbox) the files are removed by the deletion events,
        // which the outbox only delivers once that transaction has committed.
        if (context.Database.CurrentTransaction is not null)
        {
            var result = await RunAsync();
            await DeleteFilesAfterCommitAsync(cancellationToken); // only without a publisher (tests and tools)
            return result;
        }
        var execution = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
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
        await DeleteFilesAfterCommitAsync(cancellationToken);
        return execution;
    }

    private async Task DeleteFilesAfterCommitAsync(CancellationToken cancellationToken)
    {
        foreach (var key in filesToDeleteAfterCommit.ToArray())
            await attachmentStorage.DeleteAsync(key, cancellationToken);
        filesToDeleteAfterCommit.Clear();
    }

    private async Task<CoachingErasureExecution> ExecuteLockedAsync(
        PersonalDataErasureExecutionRequestedV1 message, CancellationToken cancellationToken)
    {
        filesToDeleteAfterCommit.Clear(); // a retried attempt starts over

        var existing = await context.CoachingErasureExecutions
            .SingleOrDefaultAsync(execution => execution.RequestId == message.RequestId, cancellationToken);
        if (existing is not null)
            return existing;

        var assessment = await context.CoachingErasureAssessments
            .SingleOrDefaultAsync(candidate => candidate.RequestId == message.RequestId, cancellationToken)
            ?? throw new CoachingErasureBlockedException(CoachingErasureBlockedException.AssessmentMissing);
        if (assessment.SubjectUserId != message.SubjectUserId || !assessment.CanProceed)
            throw new CoachingErasureBlockedException(assessment.HasActiveLegalHold
                ? CoachingErasureBlockedException.LegalHold
                : CoachingErasureBlockedException.AssessmentDenied);

        var completedAt = timeProvider.GetUtcNow().UtcDateTime;
        var hasActiveLegalHold = await context.CoachingLegalHolds.AnyAsync(
            hold => hold.SubjectUserId == message.SubjectUserId
                && hold.PlacedAt <= completedAt
                && (!hold.ReleasedAt.HasValue || hold.ReleasedAt > completedAt),
            cancellationToken);
        if (hasActiveLegalHold)
            throw new CoachingErasureBlockedException(CoachingErasureBlockedException.LegalHold);

        var attachments = await context.AssignmentSubmissionAttachments
            .Include(attachment => attachment.AssignmentStudent)
            .Where(attachment => attachment.AssignmentStudent.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        // Files are only removed once the rows are gone for good: a rolled-back erasure keeps both.
        if (events is not null)
        {
            foreach (var attachment in attachments)
                await events.PublishAsync(new AssignmentAttachmentDeletionRequested(
                    attachment.AssignmentStudent.AssignmentId, attachment.Id, attachment.StorageKey), cancellationToken);
        }

        // Financial records are kept unchanged for the retention period and anonymized afterwards.
        var retainedFinancialRecords = await CoachingFinancialRetention.CountAsync(context, message.SubjectUserId, cancellationToken);
        var seatsToRelease = await context.CoachingSubscriptionSeats
            .Where(seat => seat.StudentId == message.SubjectUserId && !seat.IsSuspended)
            .ToListAsync(cancellationToken);
        foreach (var seat in seatsToRelease)
        {
            seat.IsSuspended = true;
            seat.SuspensionReason = CoachingFinancialRetention.ErasedSeatReason;
        }

        var assignmentStudents = await context.AssignmentStudents
            .Where(row => row.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var examResults = await context.ExamResults
            .Where(row => row.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var studentExams = await context.Exams.Where(x => x.StudentOwnerId == message.SubjectUserId).ToListAsync(cancellationToken);
        var studyTasks = await context.StudyPlanTasks.Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var studyPlans = await context.StudyPlanRevisions.Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var planIds = studyPlans.Select(x => x.Id).ToArray();
        var practiceApprovals = await context.Set<IdempotencyRecord>().Where(x => x.Scope == "coaching.practice-review.apply"
            && Enumerable.Contains(planIds, x.ResourceId)).ToListAsync(cancellationToken);
        var studyAvailability = await context.StudyAvailability.Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var weeklyDrafts = await context.Set<WeeklyStudyDraft>().Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var weeklyReflectionReviews = await context.Set<WeeklyReflectionReview>().Where(x => x.StudentId == message.SubjectUserId || x.TeacherId == message.SubjectUserId).ToListAsync(cancellationToken);
        var weeklyReflections = await context.Set<WeeklyReflection>().Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var weeklyDecisions = await context.Set<WeeklyDecision>().Where(x => x.StudentId == message.SubjectUserId || x.TeacherId == message.SubjectUserId || x.ResponsibleTeacherId == message.SubjectUserId).ToListAsync(cancellationToken);
        var decisionWorkRequests = await CoachingStudyPlanErasureMessages.DecisionWorkRequests(context, message.SubjectUserId).ToListAsync(cancellationToken);
        var studyMessages = await CoachingStudyPlanErasureMessages.ForStudent(context, message.SubjectUserId).ToListAsync(cancellationToken);
        // Roadmap O3: a conversation belongs to its two participants; erasing either removes it with its messages and files.
        var conversations = await context.Set<CoachingConversation>()
            .Where(x => x.TeacherUserId == message.SubjectUserId || x.CounterpartUserId == message.SubjectUserId).ToListAsync(cancellationToken);
        var (conversationRecords, conversationFiles) = await Coaching.Infrastructure.Management.CoachingMessagingService
            .RemoveConversationsAsync(context, conversations, cancellationToken);
        if (events is not null)
            foreach (var file in conversationFiles)
                await events.PublishAsync(new AssignmentAttachmentDeletionRequested(file.ConversationId, file.Id, file.StorageKey), cancellationToken);
        var reminders = await context.CoachingReminderReceipts.Where(x => x.RecipientUserId == message.SubjectUserId).ToListAsync(cancellationToken);
        // Roadmap O6: the user's private calendar links stop working.
        context.CoachingCalendarFeeds.RemoveRange(await context.CoachingCalendarFeeds.Where(x => x.UserId == message.SubjectUserId).ToListAsync(cancellationToken));
        var fees = await context.CoachingFeeAgreements.Where(x => x.StudentId == message.SubjectUserId).ToListAsync(cancellationToken);
        var financeCount = fees.Count
            + await context.CoachingFeeInstallments.CountAsync(x => context.CoachingFeeAgreements.Any(fee => fee.Id == x.AgreementId && fee.StudentId == message.SubjectUserId), cancellationToken)
            + await context.CoachingFeePayments.CountAsync(x => context.CoachingFeeAgreements.Any(fee => fee.Id == x.AgreementId && fee.StudentId == message.SubjectUserId), cancellationToken);
        var goals = await context.AcademicGoals
            .Where(row => row.StudentId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var primaryGoals = await context.Set<Coaching.Domain.Entities.StudentPrimaryGoal>()
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
            + studentExams.Count + studyTasks.Count + studyPlans.Count + practiceApprovals.Count + studyAvailability.Count + weeklyDrafts.Count + weeklyReflectionReviews.Count + weeklyReflections.Count + weeklyDecisions.Count + decisionWorkRequests.Count + studyMessages.Count + reminders.Count + financeCount
            + goals.Count + primaryGoals.Count
            + attendances.Count
            + acknowledgements.Count
            + conversationRecords;
        var execution = CoachingErasureExecution.Complete(
            message.RequestId, deletedRecordCount, completedAt, message.SubjectUserId, retainedFinancialRecords);

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
        context.RemoveRange(practiceApprovals);
        context.StudyAvailability.RemoveRange(studyAvailability);
        context.RemoveRange(weeklyDrafts);
        context.RemoveRange(weeklyReflectionReviews);
        context.RemoveRange(weeklyReflections);
        context.RemoveRange(weeklyDecisions);
        context.RemoveRange(decisionWorkRequests);
        context.RemoveRange(studyMessages);
        context.CoachingReminderReceipts.RemoveRange(reminders);
        context.CoachingFeeAgreements.RemoveRange(fees);
        context.RemoveRange(primaryGoals);
        context.AcademicGoals.RemoveRange(goals);
        context.SessionAttendances.RemoveRange(attendances);
        context.CoachingAgreementAcknowledgements.RemoveRange(acknowledgements);
        context.CoachingErasureAssessments.Remove(assessment);
        context.CoachingErasureExecutions.Add(execution);
        await context.SaveChangesAsync(cancellationToken);
        if (events is null) filesToDeleteAfterCommit.AddRange(attachments.Select(attachment => attachment.StorageKey)
            .Concat(conversationFiles.Select(file => file.StorageKey)));
        return execution;
    }
}
