using SpeedReading.Application.Parents;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingParentReports(OwnedSpeedReadingDbContext db) : ISpeedReadingParentReports
{
    private readonly OwnedSpeedReadingDbContext db = db;

    public Task<SpeedReadingParentOverview> GetOverviewAsync(Guid studentUserId, DateTime nowUtc, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<SpeedReadingParentWeeklySummary?> GetWeeklySummaryAsync(Guid studentUserId, DateOnly weekStart, DateTime nowUtc,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
