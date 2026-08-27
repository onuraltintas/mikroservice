using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingAgeGroupBackfillResult(
    int AgeGroupsInserted,
    int ExistingRows,
    DateTime CompletedAtUtc);

/// <summary>
/// Copies age-group configuration into the owned Speed Reading store. The
/// identifiers remain stable because program templates and catalog entries
/// reference them without a cross-database foreign key.
/// </summary>
public sealed class OwnedSpeedReadingAgeGroupBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedSpeedReadingAgeGroupBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await owned.Database.MigrateAsync(cancellationToken);

        var sourceRows = await legacy.AgeGroupConfigurations
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        if (sourceRows.Any(item => item.Id == Guid.Empty))
            throw new InvalidOperationException("Age group source contains an empty identifier.");
        if (sourceRows.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Age group source contains duplicate names.");

        await using var transaction = await owned.Database.BeginTransactionAsync(cancellationToken);
        var existingIds = await owned.AgeGroupConfigurations
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var inserted = 0;
        var existing = 0;

        foreach (var source in sourceRows)
        {
            if (existingIds.Contains(source.Id))
            {
                existing++;
                continue;
            }

            owned.AgeGroupConfigurations.Add(AgeGroupConfiguration.Import(
                source.Id,
                source.Name,
                source.DisplayName,
                source.MinAge,
                source.MaxAge,
                source.MinWPM,
                source.RecommendedWPM,
                source.MaxWPM,
                source.RecommendedComprehension,
                source.RecommendedDailyMinutes,
                source.DefaultDifficultyLevel,
                source.OrderIndex,
                source.IsActive,
                source.Description,
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            inserted++;
        }

        await owned.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OwnedSpeedReadingAgeGroupBackfillResult(
            inserted,
            existing,
            DateTime.UtcNow);
    }

    private static string? ToAuditValue(Guid? value) => value?.ToString();

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue ? NormalizeUtc(value.Value) : null;
}
