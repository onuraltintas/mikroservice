using StackExchange.Redis;

namespace EduPlatform.Gateway.Middlewares;

/// <summary>
/// Applies a Redis-backed fixed-window limit to the public authentication and support routes.
/// The route metadata limiter remains as a local fallback when Redis is unavailable.
/// </summary>
public sealed class DistributedRateLimitingMiddleware
{
    private const string IncrementScript = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return current
        """;

    private readonly RequestDelegate _next;
    private readonly Lazy<IConnectionMultiplexer> _redis;
    private readonly ILogger<DistributedRateLimitingMiddleware> _logger;

    public DistributedRateLimitingMiddleware(
        RequestDelegate next,
        Lazy<IConnectionMultiplexer> redis,
        ILogger<DistributedRateLimitingMiddleware> logger)
    {
        _next = next;
        _redis = redis;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var rule = GetRule(context.Request.Path);
        if (rule is null)
        {
            await _next(context);
            return;
        }

        var clientAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"EduPlatform:RateLimit:{rule.Name}:{clientAddress}";

        try
        {
            var database = _redis.Value.GetDatabase();
            var countResult = await database.ScriptEvaluateAsync(
                IncrementScript,
                new RedisKey[] { key },
                new RedisValue[] { (long)rule.Window.TotalMilliseconds });

            if ((long)countResult > rule.PermitLimit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = ((int)rule.Window.TotalSeconds).ToString();
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Rate limit exceeded."
                });
                return;
            }
        }
        catch (RedisException ex)
        {
            // The endpoint metadata limiter remains active as a local fallback.
            _logger.LogError(ex, "Distributed rate limiter is unavailable for {Route}", context.Request.Path);
        }

        await _next(context);
    }

    private static RateLimitRule? GetRule(PathString path)
    {
        if (path.StartsWithSegments("/api/auth"))
        {
            return new RateLimitRule("auth", PermitLimit: 30, TimeSpan.FromMinutes(1));
        }

        if (string.Equals(path.Value, "/api/support/submit", StringComparison.OrdinalIgnoreCase))
        {
            return new RateLimitRule("support-submit", PermitLimit: 10, TimeSpan.FromMinutes(1));
        }

        return null;
    }

    private sealed record RateLimitRule(string Name, int PermitLimit, TimeSpan Window);
}
