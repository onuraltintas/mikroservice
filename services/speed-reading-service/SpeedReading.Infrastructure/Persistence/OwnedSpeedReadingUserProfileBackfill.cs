using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingUserProfileBackfillResult(
    int ProfilesInserted,
    int ExistingRows,
    DateTime CompletedAtUtc);

/// <summary>
/// Copies Speed Reading-specific user metrics out of the legacy database.
/// Authentication and identity remain owned by the Identity service; this
/// table owns only bounded-context preferences and progress targets.
/// </summary>
public sealed class OwnedSpeedReadingUserProfileBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedSpeedReadingUserProfileBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await owned.Database.MigrateAsync(cancellationToken);

        var sourceRows = await legacy.Users
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var ageGroupIds = await owned.AgeGroupConfigurations
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);

        foreach (var source in sourceRows)
        {
            if (source.Id == Guid.Empty)
                throw new InvalidOperationException("Speed Reading user profile source contains an empty identifier.");
            if (source.AgeGroupConfigurationId.HasValue
                && !ageGroupIds.Contains(source.AgeGroupConfigurationId.Value))
            {
                throw new InvalidOperationException(
                    $"Speed Reading user {source.Id} references missing age group {source.AgeGroupConfigurationId}.");
            }
        }

        await using var transaction = await owned.Database.BeginTransactionAsync(cancellationToken);
        var existingIds = await owned.UserProfiles
            .AsNoTracking()
            .Select(item => item.UserId)
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

            owned.UserProfiles.Add(SpeedReadingUserProfile.Import(
                Guid.NewGuid(),
                source.Id,
                source.CurrentLevel,
                source.TargetWPM,
                source.TargetComprehension,
                source.DailyGoalMinutes,
                source.AgeGroupConfigurationId,
                source.InstitutionId,
                isActive: true,
                NormalizeUtc(DateTime.UtcNow),
                source.Id.ToString(),
                null,
                null));
            inserted++;
        }

        await owned.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OwnedSpeedReadingUserProfileBackfillResult(
            inserted,
            existing,
            DateTime.UtcNow);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
