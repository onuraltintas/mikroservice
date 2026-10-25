using System.Globalization;
using EduPlatform.Gateway.Http;
using Microsoft.Extensions.Options;

namespace EduPlatform.Gateway.RateLimiting;

/// <summary>
/// Applies the matching route rule and, for authenticated callers, the per-user budget.
/// Runs after authentication so the user partition is the verified subject.
/// </summary>
public sealed class GatewayRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly GatewayRateLimiter _limiter;
    private readonly GatewayMetrics _metrics;
    private readonly GatewayRateLimitOptions _options;
    private readonly RateLimitRuleSet _rules;

    public GatewayRateLimitingMiddleware(
        RequestDelegate next,
        GatewayRateLimiter limiter,
        GatewayMetrics metrics,
        IOptions<GatewayRateLimitOptions> options)
    {
        _next = next;
        _limiter = limiter;
        _metrics = metrics;
        _options = options.Value;
        _rules = new RateLimitRuleSet(_options.Rules);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        RateLimitDecision? advertised = null;

        var rule = _rules.Match(context.Request.Method, context.Request.Path);
        if (rule is not null)
        {
            var decision = await _limiter.AcquireAsync(
                rule.Name,
                ClientPartition.Resolve(context, rule.Partition),
                rule.PermitLimit,
                rule.Window,
                context.RequestAborted);
            if (!decision.Allowed)
            {
                await RejectAsync(context, rule.Name, decision);
                return;
            }

            advertised = decision;
        }

        var user = ClientPartition.ForUser(context.User);
        var userOptions = _options.AuthenticatedUser;
        if (user is not null
            && userOptions.Enabled
            && !userOptions.ExcludedPathPrefixes.Any(prefix =>
                context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var decision = await _limiter.AcquireAsync(
                GatewayUserRateLimitOptions.RuleName,
                user,
                userOptions.PermitLimit,
                userOptions.Window,
                context.RequestAborted);
            if (!decision.Allowed)
            {
                await RejectAsync(context, GatewayUserRateLimitOptions.RuleName, decision);
                return;
            }

            advertised ??= decision;
        }

        if (advertised is { } budget)
        {
            WriteHeaders(context, budget);
        }

        await _next(context);
    }

    private Task RejectAsync(HttpContext context, string rule, RateLimitDecision decision)
    {
        _metrics.RateLimitRejected(rule, decision.Store);
        WriteHeaders(context, decision);
        var retryAfter = decision.RetryAfter > TimeSpan.Zero ? decision.RetryAfter : decision.ResetAfter;
        return GatewayProblemDetails.WriteRateLimitExceededAsync(context, retryAfter, rule);
    }

    private static void WriteHeaders(HttpContext context, RateLimitDecision decision)
    {
        var headers = context.Response.Headers;
        headers["RateLimit-Limit"] = decision.Limit.ToString(CultureInfo.InvariantCulture);
        headers["RateLimit-Remaining"] = decision.Remaining.ToString(CultureInfo.InvariantCulture);
        headers["RateLimit-Reset"] = Math.Max(1, (int)Math.Ceiling(decision.ResetAfter.TotalSeconds))
            .ToString(CultureInfo.InvariantCulture);
    }
}
