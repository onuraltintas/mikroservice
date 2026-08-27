using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingIdempotencyCleaner(SpeedReadingDbContext db)
    : ISpeedReadingIdempotencyCleaner
{
    public Task<int> DeleteExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken) =>
        db.IdempotencyRecords
            .Where(item => item.CreatedAt < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
}
