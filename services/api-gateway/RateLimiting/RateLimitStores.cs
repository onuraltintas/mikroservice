using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace EduPlatform.Gateway.RateLimiting;

public enum RateLimitStoreKind
{
    Redis,
    Local
}

/// <param name="Allowed">Whether the request may proceed.</param>
/// <param name="Limit">The permit limit of the window.</param>
/// <param name="Remaining">Permits left in the current window (never negative).</param>
/// <param name="ResetAfter">Time until the current window ends.</param>
/// <param name="RetryAfter">Suggested wait before retrying a rejected request.</param>
public readonly record struct RateLimitDecision(
    bool Allowed,
    int Limit,
    int Remaining,
    TimeSpan ResetAfter,
    TimeSpan RetryAfter,
    RateLimitStoreKind Store);

/// <summary>
/// Shared sliding-window counter (Cloudflare-style approximation): the previous fixed
/// window is weighted by how much of it still overlaps the sliding window, so clients
/// cannot double their rate around a window boundary. Two small integers per partition.
/// </summary>
public sealed class RedisSlidingWindowStore
{
    // KEYS[1] current window counter, KEYS[2] previous window counter.
    // ARGV[1] limit, ARGV[2] window length (ms), ARGV[3] elapsed time in the current window (ms).
    private const string Script = """
        local limit = tonumber(ARGV[1])
        local window = tonumber(ARGV[2])
        local elapsed = tonumber(ARGV[3])
        local current = tonumber(redis.call('GET', KEYS[1]) or '0')
        local previous = tonumber(redis.call('GET', KEYS[2]) or '0')
        local carried = math.floor(previous * (window - elapsed) / window)
        if carried + current >= limit then
            return {0, carried + current}
        end
        current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('PEXPIRE', KEYS[1], window * 2)
        end
        return {1, carried + current}
        """;

    private readonly Lazy<IConnectionMultiplexer> _redis;
    private readonly TimeProvider _timeProvider;
    private readonly string _keyPrefix;

    public RedisSlidingWindowStore(Lazy<IConnectionMultiplexer> redis, TimeProvider timeProvider, string keyPrefix)
    {
        _redis = redis;
        _timeProvider = timeProvider;
        _keyPrefix = keyPrefix;
    }

    public async Task<RateLimitDecision> TryAcquireAsync(
        string rule,
        string partition,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var windowMilliseconds = (long)window.TotalMilliseconds;
        var now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var windowIndex = now / windowMilliseconds;
        var elapsed = now - (windowIndex * windowMilliseconds);
        var baseKey = $"{_keyPrefix}:{rule}:{partition}:";

        var result = (RedisResult[]?)await _redis.Value.GetDatabase().ScriptEvaluateAsync(
            Script,
            [
                (RedisKey)(baseKey + windowIndex.ToString(CultureInfo.InvariantCulture)),
                (RedisKey)(baseKey + (windowIndex - 1).ToString(CultureInfo.InvariantCulture))
            ],
            [limit, windowMilliseconds, elapsed]);

        var allowed = result is not null && (long)result[0] == 1;
        var used = result is null ? limit : (long)result[1];
        var resetAfter = TimeSpan.FromMilliseconds(windowMilliseconds - elapsed);
        return new RateLimitDecision(
            allowed,
            limit,
            (int)Math.Max(0, limit - used),
            resetAfter,
            allowed ? TimeSpan.Zero : resetAfter,
            RateLimitStoreKind.Redis);
    }
}

/// <summary>
/// Per-instance sliding window used while Redis is unavailable. With N gateway replicas
/// the effective limit is at most N times the configured one, which is still bounded.
/// </summary>
public sealed class LocalSlidingWindowStore : IDisposable
{
    private const int SegmentsPerWindow = 6;
    private readonly ConcurrentDictionary<(int Limit, long WindowTicks), PartitionedRateLimiter<string>> _limiters = new();

    public RateLimitDecision TryAcquire(string rule, string partition, int limit, TimeSpan window)
    {
        var limiter = _limiters.GetOrAdd((limit, window.Ticks), static key =>
            PartitionedRateLimiter.Create<string, string>(resource =>
                RateLimitPartition.GetSlidingWindowLimiter(resource, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = key.Limit,
                    Window = TimeSpan.FromTicks(key.WindowTicks),
                    SegmentsPerWindow = SegmentsPerWindow,
                    QueueLimit = 0,
                    AutoReplenishment = true
                })));
        var resource = $"{rule}:{partition}";

        using var lease = limiter.AttemptAcquire(resource);
        var segment = TimeSpan.FromTicks(window.Ticks / SegmentsPerWindow);
        var remaining = (int)Math.Max(0, limiter.GetStatistics(resource)?.CurrentAvailablePermits ?? 0);
        var retryAfter = lease.IsAcquired
            ? TimeSpan.Zero
            : lease.TryGetMetadata(MetadataName.RetryAfter, out var hint) ? hint : segment;

        return new RateLimitDecision(lease.IsAcquired, limit, remaining, segment, retryAfter, RateLimitStoreKind.Local);
    }

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
        {
            limiter.Dispose();
        }

        _limiters.Clear();
    }
}
