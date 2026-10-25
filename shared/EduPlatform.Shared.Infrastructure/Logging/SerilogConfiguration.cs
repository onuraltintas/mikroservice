using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using EduPlatform.Shared.Infrastructure.Middleware;
using Serilog;
using System.Text.RegularExpressions;

namespace EduPlatform.Shared.Infrastructure.Logging;

/// <summary>
/// Request logging shared by the services (service-wide Serilog setup is in LoggerExtensions).
/// </summary>
public static class SerilogConfiguration
{
    private static readonly Regex GuidPathSegment = new(
        "(?i)(?<![0-9a-f])[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}(?![0-9a-f])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NumericPathSegment = new(
        @"(?<=/)[0-9]+(?=/|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Adds request logging middleware
    /// </summary>
    public static void UseRequestLogging(this WebApplication app)
    {
        app.UseCorrelationId();
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {Operation} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var safePath = CreateSafePath(httpContext);
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
                diagnosticContext.Set(
                    CorrelationIdMiddleware.HeaderName,
                    httpContext.TraceIdentifier);
                diagnosticContext.Set("RequestPath", safePath);
                diagnosticContext.Set(
                    "Operation",
                    $"{httpContext.Request.Method} {safePath}");
                diagnosticContext.Set(
                    "EventType",
                    "http.request");
                diagnosticContext.Set(
                    "Authenticated",
                    httpContext.User.Identity?.IsAuthenticated == true);
            };
        });
    }

    internal static string CreateSafePath(HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value ?? "/";
        path = GuidPathSegment.Replace(path, "{id}");
        path = NumericPathSegment.Replace(path, "{id}");
        return path;
    }
}
