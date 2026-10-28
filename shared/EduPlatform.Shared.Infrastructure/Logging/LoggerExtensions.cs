using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EduPlatform.Shared.Infrastructure.Logging;

public static class LoggerExtensions
{
    public static IHostBuilder UseCustomSerilog(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((context, services, configuration) =>
        {
            var env = context.HostingEnvironment;
            var appName = env.ApplicationName;

            // Seq only where one is configured (or the local default during development);
            // in containers the old localhost default pointed at nothing.
            var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL");
            if (string.IsNullOrWhiteSpace(seqUrl) && env.IsDevelopment())
            {
                seqUrl = "http://localhost:5341";
            }

            // LOG_FORMAT=json writes one compact JSON object per event (for Loki/ELK
            // ingestion); the default stays the human-readable console format.
            var jsonConsole = string.Equals(
                Environment.GetEnvironmentVariable("LOG_FORMAT"),
                "json",
                StringComparison.OrdinalIgnoreCase);

            // Basic Configuration
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                // Roadmap P4: readiness errors while the bus is still connecting are not faults.
                .Filter.ByExcluding(StartupHealthCheckNoise.ShouldDrop)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", appName)
                .Enrich.WithProperty("Environment", env.EnvironmentName);

            if (jsonConsole)
            {
                configuration.WriteTo.Console(new Serilog.Formatting.Compact.RenderedCompactJsonFormatter());
            }
            else
            {
                configuration.WriteTo.Console();
            }

            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                configuration.WriteTo.Seq(seqUrl);
            }
        });

        return hostBuilder;
    }
}
