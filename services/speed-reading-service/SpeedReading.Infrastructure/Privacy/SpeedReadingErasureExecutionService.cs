using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Privacy;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Privacy;

public sealed class SpeedReadingErasureExecutionService(
    OwnedSpeedReadingDbContext context,
    ISpeedReadingPrivacyInventoryRepository inventoryRepository,
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

        var inventory = await inventoryRepository.CountByUserAsync(
            message.SubjectUserId, cancellationToken);
        if (inventory.GetValueOrDefault("financialRecords") > 0)
            throw new InvalidOperationException("SpeedReading erasure is blocked by financial retention.");

        var userId = message.SubjectUserId;
        var deleted = 0;
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
        deleted += await RemoveAsync(legacy.UserSubscriptions.Where(item => item.UserId == userId), cancellationToken);
        deleted += await RemoveAsync(context.UserProfiles.Where(item => item.UserId == userId), cancellationToken);

        var execution = SpeedReadingErasureExecutionReceipt.Complete(
            message.RequestId, deleted, timeProvider.GetUtcNow().UtcDateTime);
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
