using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Privacy;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Privacy;

public sealed class SpeedReadingErasureExecutionService(
    OwnedSpeedReadingDbContext context,
    ISpeedReadingUserDirectory userDirectory,
    TimeProvider timeProvider) : ISpeedReadingErasureExecutionService
{
    public async Task<SpeedReadingErasureExecutionResult> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken)
    {
        if (message.Scope is not (PersonalDataScope.Account or PersonalDataScope.SpeedReading))
            throw new InvalidOperationException("The erasure scope does not include SpeedReading.");

        var existing = await context.ErasureExecutions
            .SingleOrDefaultAsync(item => item.RequestId == message.RequestId, cancellationToken);
        if (existing is not null)
            return ToResult(existing);

        // Review H2b: rows keyed only by e-mail (newsletter, contact form, campaign log) are found through the
        // account's address; Identity keeps the account until every service has finished.
        var userId = message.SubjectUserId;
        var directory = await userDirectory.GetUsersAsync([userId], cancellationToken);
        var email = directory.Users.FirstOrDefault(user => user.UserId == userId)?.Email?.Trim().ToLowerInvariant();
        var invitationEmail = email is null ? null : SpeedReading.Domain.Institutions.SpeedReadingInvitation.NormalizeEmail(email);
        var deleted = 0;
        deleted += await RemoveAsync(context.ProgramProgressionPolicies.Where(item => userId != Guid.Empty && item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.ProgramContinuations.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.ReviewCompletions.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.ExerciseSessionAnswers
            .Where(item => context.ExerciseSessions.Any(session => session.Id == item.SessionId && session.StudentId == userId)), cancellationToken);
        deleted += await RemoveAsync(context.ExerciseSessionResults.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.ExerciseSessions.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.ReadingSessionAnswers
            .Where(item => context.ReadingSessions.Any(session => session.Id == item.SessionId && session.UserId == userId)), cancellationToken);
        deleted += await RemoveAsync(context.ReadingSessions.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.StudentReadingAttempts.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.AssessmentAttemptExercises
            .Where(item => context.AssessmentAttempts.Any(attempt => attempt.Id == item.AssessmentAttemptId && attempt.StudentId == userId)), cancellationToken);
        deleted += await RemoveAsync(context.AssessmentAttempts.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.AssessmentStudyEnrollments.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.StudentAssignments.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.DailyExerciseLogs.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.StudentProgramProgresses.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.PersonalizedLearningPathItems.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.StudentLearningNodeProgresses.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.StudentLearningPathProgresses.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.UserAchievements.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.UserGamifications.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.UserVocabularyProgresses.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.ReviewItems.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.ContentFeedbacks.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.AdaptiveContentRecommendations.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.AdaptiveDailyGoals.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.AdaptiveLearningProfiles.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.AdaptiveTextRecommendationHistories.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.AdaptiveReadingProfiles.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(context.ReportSnapshots.Where(item => item.GeneratedForUserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.ScheduledReports.Where(item => item.UserId == userId), cancellationToken);

        var legacy = (ISpeedReadingDataContext)context;
        deleted += await RemoveAsync(legacy.AnnouncementUserInteractions.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(legacy.Notifications.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(legacy.NotificationTypePreferences.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(legacy.NotificationPreferences.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(legacy.PushSubscriptions.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(legacy.InstitutionAccessActions.Where(item => item.StudentId == userId), cancellationToken);
        deleted += await RemoveAsync(legacy.RsvpSessions.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.InstitutionMemberships.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.TeacherStudentAssignments
            .Where(item => item.StudentUserId == userId || item.TeacherUserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.Invitations.Where(item => item.AcceptedByUserId == userId
            || item.InvitedByUserId == userId
            || (invitationEmail != null && item.NormalizedEmail == invitationEmail)), cancellationToken);
        if (email is not null)
        {
            deleted += await RemoveAsync(legacy.NewsletterSubscribers.Where(item => item.Email.ToLower() == email), cancellationToken);
            deleted += await RemoveAsync(legacy.ContactMessages.Where(item => item.Email.ToLower() == email), cancellationToken);
            deleted += await RemoveAsync(legacy.EmailCampaignLogs.Where(item => item.RecipientEmail.ToLower() == email), cancellationToken);
        }
        // Review H2b: attempts that moved no money (failed, pending, rejected) are erased. Paid records stay (below).
        deleted += await RemoveAsync(SpeedReadingFinancialRetention.UnpaidPayments(legacy, userId), cancellationToken);
        deleted += await RemoveAsync(SpeedReadingFinancialRetention.UnapprovedTransfers(legacy, userId), cancellationToken);
        deleted += await RemoveAsync(context.UserProfiles.Where(item => item.UserId == userId), cancellationToken);

        var retained = await SpeedReadingFinancialRetention.CountAsync(legacy, userId, cancellationToken);
        var execution = SpeedReadingErasureExecutionReceipt.Complete(
            message.RequestId, deleted, timeProvider.GetUtcNow().UtcDateTime, userId, Math.Max(0, retained));
        context.ErasureExecutions.Add(execution);
        await context.SaveChangesAsync(cancellationToken);
        return ToResult(execution);
    }

    private async Task<int> RemoveAsync<TEntity>(
        IQueryable<TEntity> query,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var items = await query.ToListAsync(cancellationToken);
        context.RemoveRange(items);
        return items.Count;
    }

    private static SpeedReadingErasureExecutionResult ToResult(
        SpeedReadingErasureExecutionReceipt receipt) =>
        new(receipt.Id, receipt.RequestId, receipt.DeletedRecordCount, receipt.CompletedAt);
}
