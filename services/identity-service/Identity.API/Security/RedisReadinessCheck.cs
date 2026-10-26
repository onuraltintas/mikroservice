using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Identity.API.Security;

/// <summary>
/// Readiness only: sign-in degrades without Redis (lockout and caches fall back), but
/// Google registration and the maintenance switches need it, so an instance without
/// Redis should not receive traffic while another one could.
/// </summary>
public sealed class RedisReadinessCheck(Lazy<IConnectionMultiplexer> redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await redis.Value.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis ping {latency.TotalMilliseconds:0} ms");
        }
        catch (Exception exception) when (exception is RedisException or ObjectDisposedException)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable.", exception);
        }
    }
}
