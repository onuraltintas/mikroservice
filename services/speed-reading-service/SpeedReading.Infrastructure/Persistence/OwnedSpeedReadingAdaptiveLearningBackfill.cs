using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingAdaptiveLearningBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedAdaptiveLearningBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var sourceProfiles = await legacy.StudentLearningProfiles.AsNoTracking().ToListAsync(cancellationToken);
        var sourceRecommendations = await legacy.ContentRecommendations.AsNoTracking().ToListAsync(cancellationToken);
        var sourceGoals = await legacy.DailyGoals.AsNoTracking().ToListAsync(cancellationToken);
        var recommendationTextIds = sourceRecommendations.Select(item => item.ReadingTextId).Distinct().ToArray();
        var ownedTextIds = await owned.ReadingTexts
            .IgnoreQueryFilters()
            .Where(item => recommendationTextIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        if (sourceRecommendations.Any(item => !ownedTextIds.Contains(item.ReadingTextId)))
            throw new InvalidOperationException("Adaptive recommendation references a missing owned reading text.");

        var existingProfileIds = await owned.AdaptiveLearningProfiles
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var existingRecommendationIds = await owned.AdaptiveContentRecommendations
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var existingGoalIds = await owned.AdaptiveDailyGoals
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var imported = 0;
        foreach (var item in sourceProfiles.Where(item => existingProfileIds.Add(item.Id)))
        {
            owned.AdaptiveLearningProfiles.Add(item);
            imported++;
        }
        foreach (var item in sourceRecommendations.Where(item => existingRecommendationIds.Add(item.Id)))
        {
            owned.AdaptiveContentRecommendations.Add(item);
            imported++;
        }
        foreach (var item in sourceGoals.Where(item => existingGoalIds.Add(item.Id)))
        {
            owned.AdaptiveDailyGoals.Add(item);
            imported++;
        }

        if (imported > 0)
            await owned.SaveChangesAsync(cancellationToken);

        return new OwnedAdaptiveLearningBackfillResult(
            sourceProfiles.Count + sourceRecommendations.Count + sourceGoals.Count,
            imported);
    }
}

public sealed record OwnedAdaptiveLearningBackfillResult(int SourceCount, int ImportedCount);
