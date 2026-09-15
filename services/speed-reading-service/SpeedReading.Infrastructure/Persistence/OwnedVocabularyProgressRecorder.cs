using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Vocabulary;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed record OwnedVocabularyReviewResult(
    bool Found,
    int CurrentBox,
    bool IsNewMastery);

internal static class OwnedVocabularyProgressRecorder
{
    public static async Task<OwnedVocabularyReviewResult> RecordAsync(
        OwnedSpeedReadingDbContext db,
        Guid userId,
        Guid vocabularyItemId,
        bool isCorrect,
        DateTime reviewedAt,
        CancellationToken cancellationToken)
    {
        var item = await db.VocabularyItems.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == vocabularyItemId && !value.IsDeleted, cancellationToken);
        if (item is null)
            return new OwnedVocabularyReviewResult(false, 0, false);

        var progress = await db.UserVocabularyProgresses
            .OrderByDescending(value => value.CreatedAt)
            .FirstOrDefaultAsync(
                value => value.UserId == userId && value.VocabularyItemId == vocabularyItemId,
                cancellationToken);
        if (progress is null)
        {
            progress = UserVocabularyProgress.Create(Guid.NewGuid(), userId, vocabularyItemId, reviewedAt);
            db.UserVocabularyProgresses.Add(progress);
        }
        else if (progress.IsDeleted)
        {
            progress.Reactivate(userId, reviewedAt);
        }

        var outcome = progress.Review(isCorrect, userId, reviewedAt);
        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(value => value.UserId == userId && !value.IsDeleted, cancellationToken);
        if (stats is null)
        {
            stats = UserGamification.CreateDefault(Guid.NewGuid(), userId, reviewedAt, userId.ToString());
            db.UserGamifications.Add(stats);
        }

        stats.RecordVerifiedVocabularyReview(
            item.Category,
            item.DifficultyLevel,
            outcome.CurrentBox,
            outcome.ConsecutiveCorrectCount,
            isCorrect,
            outcome.IsNewMastery,
            userId,
            reviewedAt);
        await OwnedGamificationAchievementEvaluator.UnlockEligibleAsync(
            db,
            stats,
            userId,
            reviewedAt,
            cancellationToken);

        return new OwnedVocabularyReviewResult(true, outcome.CurrentBox, outcome.IsNewMastery);
    }
}
