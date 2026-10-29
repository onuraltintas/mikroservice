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
    /// <summary>
    /// Records a whole quiz in answer order with one read of the words, one of the student's progress and one of
    /// gamification (decision 29.10.2026); the outcome matches calling <see cref="RecordAsync"/> per answer.
    /// Returns false, without recording anything, when a word is no longer available.
    /// </summary>
    public static async Task<bool> RecordManyAsync(
        OwnedSpeedReadingDbContext db,
        Guid userId,
        List<(Guid VocabularyItemId, bool IsCorrect)> answers,
        DateTime reviewedAt,
        bool awardVerifiedGamification,
        CancellationToken cancellationToken)
    {
        if (answers.Count == 0) return true;
        var itemIds = answers.Select(answer => answer.VocabularyItemId).Distinct().ToList();
        var items = await db.VocabularyItems.AsNoTracking()
            .Where(value => itemIds.Contains(value.Id) && !value.IsDeleted)
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        if (items.Count != itemIds.Count) return false;

        var progressRows = await db.UserVocabularyProgresses
            .Where(value => value.UserId == userId && itemIds.Contains(value.VocabularyItemId))
            .ToListAsync(cancellationToken);
        var progressByItem = progressRows
            .GroupBy(value => value.VocabularyItemId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(value => value.CreatedAt).First());

        UserGamification? stats = null;
        if (awardVerifiedGamification)
        {
            stats = db.UserGamifications.Local.SingleOrDefault(value => value.UserId == userId && !value.IsDeleted)
                ?? await db.UserGamifications.SingleOrDefaultAsync(value => value.UserId == userId && !value.IsDeleted, cancellationToken);
            if (stats is null)
            {
                stats = UserGamification.CreateDefault(Guid.NewGuid(), userId, reviewedAt, userId.ToString());
                db.UserGamifications.Add(stats);
            }
        }

        foreach (var (itemId, isCorrect) in answers)
        {
            var item = items[itemId];
            if (!progressByItem.TryGetValue(itemId, out var progress))
            {
                progress = UserVocabularyProgress.Create(Guid.NewGuid(), userId, itemId, reviewedAt);
                db.UserVocabularyProgresses.Add(progress);
                progressByItem[itemId] = progress;
            }
            else if (progress.IsDeleted)
            {
                progress.Reactivate(userId, reviewedAt);
            }

            var outcome = progress.Review(isCorrect, userId, reviewedAt);
            stats?.RecordVerifiedVocabularyReview(
                item.Category,
                item.DifficultyLevel,
                outcome.CurrentBox,
                outcome.ConsecutiveCorrectCount,
                isCorrect,
                outcome.IsNewMastery,
                userId,
                reviewedAt);
        }

        if (stats is not null)
            await OwnedGamificationAchievementEvaluator.UnlockEligibleAsync(db, stats, userId, reviewedAt, cancellationToken);
        return true;
    }

    public static async Task<OwnedVocabularyReviewResult> RecordAsync(
        OwnedSpeedReadingDbContext db,
        Guid userId,
        Guid vocabularyItemId,
        bool isCorrect,
        DateTime reviewedAt,
        bool awardVerifiedGamification,
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
        if (!awardVerifiedGamification)
            return new OwnedVocabularyReviewResult(true, outcome.CurrentBox, outcome.IsNewMastery);

        var stats = db.UserGamifications.Local.SingleOrDefault(value => value.UserId == userId && !value.IsDeleted)
            ?? await db.UserGamifications.SingleOrDefaultAsync(value => value.UserId == userId && !value.IsDeleted, cancellationToken);
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
