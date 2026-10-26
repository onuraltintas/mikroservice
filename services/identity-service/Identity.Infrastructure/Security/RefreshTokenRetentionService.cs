using Identity.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

/// <summary>Section <c>Identity:Session</c> (retention part).</summary>
public sealed class RefreshTokenRetentionOptions
{
    public const string SectionName = "Identity:Session";

    /// <summary>
    /// Expired or revoked tokens are kept this long before deletion. Revoked rotated
    /// tokens are what reuse detection looks up, so keep this at least as long as a
    /// refresh token lives.
    /// </summary>
    public int RetentionDays { get; set; } = 14;

    public int PurgeIntervalMinutes { get; set; } = 60;

    public int PurgeBatchSize { get; set; } = 5_000;

    public void Validate()
    {
        if (RetentionDays is < 1 or > 365)
            throw new InvalidOperationException($"{SectionName}:RetentionDays must be between 1 and 365.");
        if (PurgeIntervalMinutes is < 1 or > 1440)
            throw new InvalidOperationException($"{SectionName}:PurgeIntervalMinutes must be between 1 and 1440.");
        if (PurgeBatchSize is < 100 or > 100_000)
            throw new InvalidOperationException($"{SectionName}:PurgeBatchSize must be between 100 and 100000.");
    }
}

/// <summary>
/// Deletes refresh tokens that expired or were revoked longer ago than the retention
/// period, in small batches so the table never holds long locks. Running on several
/// replicas at once is harmless: each batch deletes whatever rows still qualify.
/// </summary>
public sealed class RefreshTokenRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RefreshTokenRetentionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefreshTokenRetentionService> _logger;

    public RefreshTokenRetentionService(
        IServiceScopeFactory scopes,
        IOptions<RefreshTokenRetentionOptions> options,
        TimeProvider timeProvider,
        ILogger<RefreshTokenRetentionService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.PurgeIntervalMinutes), _timeProvider);
        do
        {
            try
            {
                var deleted = await PurgeOnceAsync(stoppingToken);
                if (deleted > 0)
                {
                    _logger.LogInformation("Deleted {Count} expired or revoked refresh tokens.", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Refresh token retention purge failed; retrying on the next cycle.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> PurgeOnceAsync(CancellationToken cancellationToken)
    {
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-_options.RetentionDays);
        var total = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var deleted = await users.PurgeRefreshTokensAsync(cutoff, _options.PurgeBatchSize, cancellationToken);
            total += deleted;
            if (deleted < _options.PurgeBatchSize)
            {
                break;
            }
        }

        return total;
    }
}
