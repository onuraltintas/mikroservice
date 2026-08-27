using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingAdaptiveTextBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedAdaptiveTextBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var sourceProfiles = await legacy.StudentReadingProfiles.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var profile in sourceProfiles)
        {
            profile.PreferredCategories = ParseCategories(profile.PreferredCategoriesSource);
            profile.DifficultCategories = ParseCategories(profile.DifficultCategoriesSource);
        }
        var sourceHistory = await legacy.TextRecommendationHistories.AsNoTracking().ToListAsync(cancellationToken);
        var historyTextIds = sourceHistory.Select(item => item.ReadingTextId).Distinct().ToArray();
        var ownedTextIds = await owned.ReadingTexts
            .IgnoreQueryFilters()
            .Where(item => historyTextIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        if (sourceHistory.Any(item => !ownedTextIds.Contains(item.ReadingTextId)))
            throw new InvalidOperationException("Adaptive text history references a missing owned reading text.");

        var existingProfileIds = await owned.AdaptiveReadingProfiles
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var existingHistoryIds = await owned.AdaptiveTextRecommendationHistories
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var imported = 0;
        foreach (var item in sourceProfiles.Where(item => existingProfileIds.Add(item.Id)))
        {
            owned.AdaptiveReadingProfiles.Add(item);
            imported++;
        }
        foreach (var item in sourceHistory.Where(item => existingHistoryIds.Add(item.Id)))
        {
            owned.AdaptiveTextRecommendationHistories.Add(item);
            imported++;
        }

        if (imported > 0)
            await owned.SaveChangesAsync(cancellationToken);

        return new OwnedAdaptiveTextBackfillResult(sourceProfiles.Count + sourceHistory.Count, imported);
    }

    private static List<string> ParseCategories(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }
        catch (JsonException)
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }
}

public sealed record OwnedAdaptiveTextBackfillResult(int SourceCount, int ImportedCount);
