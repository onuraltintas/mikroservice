using StackExchange.Redis;

namespace EduPlatform.Gateway.RateLimiting;

/// <summary>
/// Uses the shared Redis window and falls back to the per-instance window when Redis
/// fails. After a failure Redis is skipped for a short back-off so an outage does not add
/// a connection timeout to every request.
/// </summary>
public sealed class GatewayRateLimiter
{
    private readonly RedisSlidingWindowStore _redis;
    private readonly LocalSlidingWindowStore _local;
    private readonly GatewayMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _backOff;
    private readonly ILogger<GatewayRateLimiter> _logger;
    private long _redisUnavailableUntilTicks;

    public GatewayRateLimiter(
        RedisSlidingWindowStore redis,
        LocalSlidingWindowStore local,
        GatewayMetrics metrics,
        TimeProvider timeProvider,
        GatewayRateLimitOptions options,
        ILogger<GatewayRateLimiter> logger)
    {
        _redis = redis;
        _local = local;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _backOff = TimeSpan.FromSeconds(options.RedisRetryAfterFailureSeconds);
        _logger = logger;
    }

    public async Task<RateLimitDecision> AcquireAsync(
        string rule,
        string partition,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcTicks;
        if (now >= Interlocked.Read(ref _redisUnavailableUntilTicks))
        {
            try
            {
                return await _redis.TryAcquireAsync(rule, partition, limit, window, cancellationToken);
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                var previous = Interlocked.Exchange(ref _redisUnavailableUntilTicks, now + _backOff.Ticks);
                if (previous <= now)
                {
                    _logger.LogWarning(
                        exception,
                        "Redis rate-limit store unavailable; using the local fallback for {BackOffSeconds}s",
                        _backOff.TotalSeconds);
                }
            }
        }

        _metrics.RateLimitFallback(rule);
        return _local.TryAcquire(rule, partition, limit, window);
    }
}
