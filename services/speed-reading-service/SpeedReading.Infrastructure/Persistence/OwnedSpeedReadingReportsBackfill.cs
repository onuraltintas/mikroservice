using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingReportsBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedReportsBackfillResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var templates = await legacy.ReportTemplates.AsNoTracking().ToListAsync(cancellationToken);
        var snapshots = await legacy.ReportSnapshots.AsNoTracking().ToListAsync(cancellationToken);
        var schedules = await legacy.ScheduledReports.AsNoTracking().ToListAsync(cancellationToken);
        var templateIds = templates.Select(item => item.Id).ToHashSet();
        if (snapshots.Any(item => !templateIds.Contains(item.ReportTemplateId))
            || schedules.Any(item => !templateIds.Contains(item.ReportTemplateId)))
            throw new InvalidOperationException("A report record references a missing report template.");

        var existingTemplateIds = await owned.ReportTemplates.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existingSnapshotIds = await owned.ReportSnapshots.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existingScheduleIds = await owned.ScheduledReports.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var imported = 0;
        foreach (var item in templates.Where(item => existingTemplateIds.Add(item.Id)))
        {
            owned.ReportTemplates.Add(item);
            imported++;
        }
        foreach (var item in snapshots.Where(item => existingSnapshotIds.Add(item.Id)))
        {
            owned.ReportSnapshots.Add(item);
            imported++;
        }
        foreach (var item in schedules.Where(item => existingScheduleIds.Add(item.Id)))
        {
            owned.ScheduledReports.Add(item);
            imported++;
        }
        if (imported > 0)
            await owned.SaveChangesAsync(cancellationToken);

        return new OwnedReportsBackfillResult(templates.Count + snapshots.Count + schedules.Count, imported);
    }
}

public sealed record OwnedReportsBackfillResult(int SourceCount, int ImportedCount);
