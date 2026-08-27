using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingContentFeedbackBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedContentFeedbackBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var source = await legacy.UserContentFeedbacks
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var existingIds = await owned.ContentFeedbacks
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var imported = 0;

        foreach (var item in source)
        {
            if (!existingIds.Add(item.Id))
                continue;

            owned.ContentFeedbacks.Add(item);
            imported++;
        }

        if (imported > 0)
            await owned.SaveChangesAsync(cancellationToken);

        return new OwnedContentFeedbackBackfillResult(source.Count, imported);
    }
}

public sealed record OwnedContentFeedbackBackfillResult(int SourceCount, int ImportedCount);
