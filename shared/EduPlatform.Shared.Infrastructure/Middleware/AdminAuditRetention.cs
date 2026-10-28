using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Shared.Infrastructure.Middleware;

/// <summary>
/// Admin audit records stay, but the client IP address and user agent are kept only for
/// <see cref="ClientDataMonths"/> months.
/// </summary>
public static class AdminAuditRetention
{
    public const int ClientDataMonths = 3;
    public const string EnabledSetting = "AdminAudit:ClientDataRetentionEnabled";

    public static DateTimeOffset ClientDataCutoff(DateTimeOffset now) => now.ToUniversalTime().AddMonths(-ClientDataMonths);

    /// <summary>Registers the service's purger and the daily worker (on unless the setting is false).</summary>
    public static IServiceCollection AddAdminAuditClientDataRetention<TPurger>(this IServiceCollection services, IConfiguration configuration)
        where TPurger : class, IAdminAuditClientDataPurger
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAdminAuditClientDataPurger, TPurger>();
        if (configuration.GetValue(EnabledSetting, true))
            services.AddHostedService<AdminAuditClientDataRetentionWorker>();
        return services;
    }
}

/// <summary>Clears ClientIp and UserAgent of the service's audit records that occurred before the cutoff.</summary>
public interface IAdminAuditClientDataPurger
{
    Task<int> ClearClientDataBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

public sealed class AdminAuditClientDataRetentionWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<AdminAuditClientDataRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var cleared = await scope.ServiceProvider.GetRequiredService<IAdminAuditClientDataPurger>()
                    .ClearClientDataBeforeAsync(AdminAuditRetention.ClientDataCutoff(clock.GetUtcNow()), stoppingToken);
                if (cleared > 0)
                    logger.LogInformation("Cleared client IP and user agent of {Count} admin audit records older than {Months} months",
                        cleared, AdminAuditRetention.ClientDataMonths);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Admin audit client data cleanup failed; the next cycle will retry."); }
            await Task.Delay(TimeSpan.FromDays(1), clock, stoppingToken);
        }
    }
}
