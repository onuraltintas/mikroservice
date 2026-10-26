using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Services;

/// <summary>
/// Shared loop for the durable delivery tables (e-mails, support forwards): claim one due
/// row under a lease, deliver it, back off when idle. Subclasses own the table-specific
/// claim query and the lease-guarded status updates.
/// </summary>
public abstract class LeasedDeliveryWorker<TWorkItem> : BackgroundService
    where TWorkItem : class
{
    public const int MaxAttempts = 12;
    protected static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    protected static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    protected LeasedDeliveryWorker(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        ScopeFactory = scopeFactory;
        Logger = logger;
    }

    protected IServiceScopeFactory ScopeFactory { get; }
    protected ILogger Logger { get; }

    /// <summary>Exponential backoff: 2, 4, 8 … seconds, at most 256 seconds.</summary>
    public static DateTime RetryAt(DateTime now, int attemptCount) =>
        now.AddSeconds(Math.Pow(2, Math.Clamp(attemptCount, 1, 8)));

    protected static string Truncate(string error) => error[..Math.Min(error.Length, 2_000)];

    /// <summary>Claims and delivers one due item; false when nothing was due.</summary>
    public async Task<bool> ProcessOnceAsync(CancellationToken cancellationToken)
    {
        var workItem = await ClaimNextAsync(cancellationToken);
        if (workItem is null) return false;
        await DeliverAsync(workItem, cancellationToken);
        return true;
    }

    protected abstract Task<TWorkItem?> ClaimNextAsync(CancellationToken cancellationToken);

    protected abstract Task DeliverAsync(TWorkItem workItem, CancellationToken cancellationToken);

    /// <summary>Housekeeping run before each claim (subclasses throttle it themselves).</summary>
    protected virtual Task RunMaintenanceAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMaintenanceAsync(stoppingToken);
                if (await ProcessOnceAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "{Worker} iteration failed", GetType().Name);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
