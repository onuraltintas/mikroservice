using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Notifications;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class SpeedReadingNewsletterCampaignScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<SpeedReadingNewsletterCampaignScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var campaigns = scope.ServiceProvider.GetRequiredService<ISpeedReadingEmailCampaigns>();
                await campaigns.ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Newsletter campaign scheduler iteration failed");
            }
        }
    }
}
