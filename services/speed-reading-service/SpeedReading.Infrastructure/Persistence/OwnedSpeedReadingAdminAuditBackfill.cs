using EduPlatform.Shared.Infrastructure.Middleware;
using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingAdminAuditBackfillResult(
    int RecordsInserted,
    int ExistingRows,
    DateTime CompletedAtUtc);

public sealed class OwnedSpeedReadingAdminAuditBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedSpeedReadingAdminAuditBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await owned.Database.MigrateAsync(cancellationToken);
        var records = await legacy.AdminAuditRecords
            .AsNoTracking()
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var existingIds = await owned.AdminAuditRecords
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var inserted = 0;
        var existing = 0;
        foreach (var record in records)
        {
            if (!existingIds.Add(record.Id))
            {
                existing++;
                continue;
            }

            owned.AdminAuditRecords.Add(record);
            inserted++;
        }

        await owned.SaveChangesAsync(cancellationToken);
        return new OwnedSpeedReadingAdminAuditBackfillResult(inserted, existing, DateTime.UtcNow);
    }
}
