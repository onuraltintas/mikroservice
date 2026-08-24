using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SpeedReading.Infrastructure;

/// <summary>
/// Keeps the additive idempotency ledger bounded. Seven days is long enough
/// for client retries while avoiding unbounded growth at platform scale.
/// </summary>
public sealed class SpeedReadingIdempotencyCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SpeedReadingIdempotencyCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            await DeleteExpiredRecordsAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeleteExpiredRecordsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SpeedReadingDbContext>();
            var cutoff = DateTime.UtcNow.Subtract(Retention);
            var deleted = await db.IdempotencyRecords
                .Where(item => item.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                logger.LogInformation(
                    "Deleted {Count} expired speed-reading idempotency records.",
                    deleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            // Cleanup must never take down the API; the next interval retries.
            logger.LogWarning(exception, "Speed-reading idempotency cleanup failed.");
        }
    }
}
