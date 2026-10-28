using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serilog.Events;

namespace EduPlatform.Shared.Infrastructure.Logging;

/// <summary>
/// Roadmap P4: right after a container starts, /health/ready reports Unhealthy until the
/// message bus and database connections are up. Those warnings and errors are expected and
/// only bury real faults, so they are dropped during a short startup window. After the
/// window every health-check event is logged again; fatal events are never dropped.
/// </summary>
public static class StartupHealthCheckNoise
{
    private const string HealthChecksSource = "Microsoft.Extensions.Diagnostics.HealthChecks";
    private static readonly Stopwatch Started = Stopwatch.StartNew();

    public static TimeSpan Window { get; } = TimeSpan.FromSeconds(90);

    public static TimeSpan Uptime => Started.Elapsed;

    public static bool ShouldDrop(LogEvent logEvent) => ShouldDrop(logEvent, Uptime);

    public static bool ShouldDrop(LogEvent logEvent, TimeSpan uptime)
    {
        if (uptime >= Window) return false;
        if (logEvent.Level is not (LogEventLevel.Warning or LogEventLevel.Error)) return false;

        return logEvent.Properties.TryGetValue("SourceContext", out var source)
            && source is ScalarValue { Value: string context }
            && context.StartsWith(HealthChecksSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Level for the request log line ("HTTP GET /health/ready responded 503"): a failing
    /// health endpoint during the startup window is logged at Debug, below the minimum
    /// level; everything else keeps Serilog's default (5xx or exception = Error).
    /// </summary>
    public static LogEventLevel RequestLevel(HttpContext context, double elapsedMs, Exception? exception) =>
        RequestLevel(context, exception, Uptime);

    public static LogEventLevel RequestLevel(HttpContext context, Exception? exception, TimeSpan uptime)
    {
        var failed = exception is not null || context.Response.StatusCode > 499;
        if (!failed) return LogEventLevel.Information;

        var startupHealthCheck = exception is null
            && uptime < Window
            && context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
        return startupHealthCheck ? LogEventLevel.Debug : LogEventLevel.Error;
    }
}
