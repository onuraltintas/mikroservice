using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using EduPlatform.Shared.Infrastructure.Middleware;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Text.RegularExpressions;

namespace EduPlatform.Shared.Infrastructure.Logging;

/// <summary>
/// Centralized Serilog configuration for all microservices
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
    /// Creates and configures Serilog logger for the application
    /// </summary>
    public static void ConfigureSerilog(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            var seqUrl = context.Configuration.GetValue<string>("Logging:SeqUrl") ?? "http://localhost:5341";
            var environment = context.HostingEnvironment.EnvironmentName;

            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithProperty("Environment", environment)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Seq(seqUrl);

            // Add file logging for production
            if (!context.HostingEnvironment.IsDevelopment())
            {
                configuration.WriteTo.File(
                    new JsonFormatter(),
                    path: $"logs/{serviceName}-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30);
            }
        });
    }

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
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
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
