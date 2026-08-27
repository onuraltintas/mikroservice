namespace SpeedReading.Infrastructure;

public interface ISpeedReadingIdempotencyCleaner
{
    Task<int> DeleteExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}
