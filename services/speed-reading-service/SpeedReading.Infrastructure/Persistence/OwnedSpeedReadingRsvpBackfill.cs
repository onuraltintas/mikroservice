using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingRsvpBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedRsvpBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var source = await legacy.RsvpSessions.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var item in source)
        {
            item.FontFamily = "Arial";
            item.FontSize = 16;
            item.BackgroundColor = "#ffffff";
            item.TextColor = "#000000";
            item.WordsPerMinute = (int)Math.Round(item.SourceAverageWpm);
            item.CompletedWords = (int)Math.Round(item.TotalWords * Math.Clamp(item.CompletionPercentage, 0, 100) / 100m);
            item.Completed = item.CompletedAt.HasValue;
        }
        var target = (ISpeedReadingDataContext)owned;
        var existingIds = await target.RsvpSessions
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);

        var imported = 0;
        foreach (var item in source)
        {
            if (!existingIds.Add(item.Id))
                continue;

            target.RsvpSessions.Add(item);
            imported++;
        }

        if (imported > 0)
            await owned.SaveChangesAsync(cancellationToken);

        return new OwnedRsvpBackfillResult(source.Count, imported);
    }
}

public sealed record OwnedRsvpBackfillResult(int SourceCount, int ImportedCount);
