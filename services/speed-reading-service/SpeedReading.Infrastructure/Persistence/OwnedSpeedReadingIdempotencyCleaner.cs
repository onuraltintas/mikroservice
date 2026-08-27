using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingIdempotencyCleaner(OwnedSpeedReadingDbContext db)
    : ISpeedReadingIdempotencyCleaner
{
    public Task<int> DeleteExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken) =>
        db.IdempotencyRecords
            .Where(item => item.CreatedAt < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
}
