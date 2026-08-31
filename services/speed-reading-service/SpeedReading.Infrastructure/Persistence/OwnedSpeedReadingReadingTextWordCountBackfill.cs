using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingReadingTextWordCountBackfill(OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedReadingTextWordCountBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var readingTexts = await owned.ReadingTexts
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var updatedCount = 0;
        foreach (var readingText in readingTexts)
        {
            if (readingText.RecalculateWordCount())
                updatedCount++;
        }

        if (updatedCount > 0)
            await owned.SaveChangesAsync(cancellationToken);

        return new OwnedReadingTextWordCountBackfillResult(
            readingTexts.Count,
            updatedCount,
            readingTexts.Count - updatedCount);
    }
}

public sealed record OwnedReadingTextWordCountBackfillResult(
    int ScannedCount,
    int UpdatedCount,
    int UnchangedCount);
