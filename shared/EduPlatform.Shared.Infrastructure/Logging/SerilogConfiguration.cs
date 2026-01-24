using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace EduPlatform.Shared.Infrastructure.Logging;

/// <summary>
/// Centralized Serilog configuration for all microservices
/// </summary>
public static class SerilogConfiguration
{
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
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                
                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
                }
            };
        });
    }
}
