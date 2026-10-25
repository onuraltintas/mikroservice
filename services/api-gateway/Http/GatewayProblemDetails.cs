using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Gateway.Http;

/// <summary>
/// Writes gateway-originated errors in the platform's RFC 9457 problem format
/// (see docs/API_CONTRACTS.md). Legacy fields that clients already read are kept
/// as extension members so the format change is backward compatible.
/// </summary>
internal static class GatewayProblemDetails
{
    public const string ContentType = "application/problem+json";
    public const string RateLimitExceededType = "https://eduplatform.dev/problems/rate-limit-exceeded";
    public const string ServiceUnavailableType = "https://eduplatform.dev/problems/service-unavailable";
    public const string PayloadTooLargeType = "https://eduplatform.dev/problems/payload-too-large";
    public const string GatewayTimeoutType = "https://eduplatform.dev/problems/gateway-timeout";

    public const string GatewayTimeoutDetail = "Servis yanıtı zamanında gelmedi. Lütfen tekrar deneyin.";

    public const string RateLimitDetail = "Çok fazla istek gönderildi. Lütfen biraz bekleyip tekrar deneyin.";

    /// <summary>Writes a 429 response. <paramref name="retryAfter"/> is rounded up to whole seconds.</summary>
    public static Task WriteRateLimitExceededAsync(HttpContext context, TimeSpan retryAfter, string? rule)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        context.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return WriteAsync(
            context,
            StatusCodes.Status429TooManyRequests,
            RateLimitExceededType,
            "Too Many Requests",
            RateLimitDetail,
            new Dictionary<string, object?>
            {
                ["error"] = "Rate limit exceeded.",
                ["message"] = RateLimitDetail,
                ["retryAfterSeconds"] = seconds,
                ["rule"] = rule
            });
    }

    public static Task WriteMaintenanceAsync(HttpContext context, string service, string message) =>
        WriteAsync(
            context,
            StatusCodes.Status503ServiceUnavailable,
            ServiceUnavailableType,
            "Service Unavailable",
            message,
            new Dictionary<string, object?>
            {
                ["error"] = "Service Unavailable",
                ["message"] = message,
                ["service"] = service,
                ["timestamp"] = DateTime.UtcNow
            });

    public static Task WriteAsync(
        HttpContext context,
        int status,
        string type,
        string title,
        string detail,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        foreach (var (key, value) in extensions ?? new Dictionary<string, object?>())
        {
            if (value is not null)
            {
                problem.Extensions[key] = value;
            }
        }

        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: ContentType);
    }
}
