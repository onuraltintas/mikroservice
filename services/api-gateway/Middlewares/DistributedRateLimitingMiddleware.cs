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
        var rule = ResolveRateLimitRule(context.Request);
        if (rule is null)
        {
            await _next(context);
            return;
        }

        var clientAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        // Forward the address resolved by the gateway's trusted-proxy middleware.
        // Never let a caller-supplied copy of this internal header reach downstream APIs.
        context.Request.Headers.Remove("X-EduPlatform-Client-IP");
        context.Request.Headers["X-EduPlatform-Client-IP"] = clientAddress;
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

    internal static RateLimitRule? ResolveRateLimitRule(HttpRequest request)
    {
        var path = request.Path;

        if (path.StartsWithSegments("/api/auth"))
        {
            if (HttpMethods.IsPost(request.Method))
            {
                if (string.Equals(path.Value, "/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path.Value, "/api/auth/resend-verification-email", StringComparison.OrdinalIgnoreCase))
                {
                    return new RateLimitRule("auth-email", PermitLimit: 5, TimeSpan.FromMinutes(15));
                }

                if (string.Equals(path.Value, "/api/auth/reset-password", StringComparison.OrdinalIgnoreCase))
                {
                    return new RateLimitRule("auth-password-reset", PermitLimit: 10, TimeSpan.FromMinutes(15));
                }

                if (string.Equals(path.Value, "/api/auth/confirm-email", StringComparison.OrdinalIgnoreCase))
                {
                    return new RateLimitRule("auth-email-confirmation", PermitLimit: 10, TimeSpan.FromMinutes(15));
                }

                if (path.StartsWithSegments("/api/auth/coaching/register")
                    || path.StartsWithSegments("/api/auth/speed-reading/register"))
                {
                    return new RateLimitRule("auth-registration", PermitLimit: 15, TimeSpan.FromMinutes(1));
                }
            }

            return new RateLimitRule("auth", PermitLimit: 30, TimeSpan.FromMinutes(1));
        }

        if (string.Equals(path.Value, "/api/support/submit", StringComparison.OrdinalIgnoreCase))
        {
            return new RateLimitRule("support-submit", PermitLimit: 10, TimeSpan.FromMinutes(1));
        }

        if (string.Equals(path.Value, "/api/speed-reading/cms/contact", StringComparison.OrdinalIgnoreCase))
        {
            return new RateLimitRule("speed-reading-public-write", PermitLimit: 8, TimeSpan.FromMinutes(10));
        }

        if (path.StartsWithSegments("/api/speed-reading/cms/newsletter")
            && HttpMethods.IsPost(request.Method))
        {
            return new RateLimitRule("speed-reading-public-write", PermitLimit: 8, TimeSpan.FromMinutes(10));
        }

        if (path.StartsWithSegments("/api/coaching/cms/newsletter")
            && HttpMethods.IsPost(request.Method))
        {
            return new RateLimitRule("coaching-newsletter-public-write", PermitLimit: 8, TimeSpan.FromMinutes(10));
        }

        if (HttpMethods.IsPost(request.Method)
            && string.Equals(path.Value, "/api/speed-reading/bank-transfer/requests", StringComparison.OrdinalIgnoreCase))
        {
            return new RateLimitRule("bank-transfer-request", PermitLimit: 5, TimeSpan.FromMinutes(10));
        }

        return null;
    }

    internal sealed record RateLimitRule(string Name, int PermitLimit, TimeSpan Window);
}
