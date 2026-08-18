using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EduPlatform.Shared.Infrastructure.Observability;

/// <summary>
/// Registers the common OpenTelemetry instrumentation used by every service.
/// Export is opt-in: development containers do not attempt to connect to a
/// collector unless OTEL_EXPORTER_OTLP_ENDPOINT is configured.
/// </summary>
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddEduPlatformOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceName)
    {
        var endpoint = GetOtlpEndpoint(configuration);
        var sampleRatio = GetTraceSampleRatio(configuration, environment);

        var openTelemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName));

        openTelemetry.WithTracing(tracing =>
        {
            tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampleRatio)))
                .AddAspNetCoreInstrumentation(options => options.RecordException = true)
                .AddHttpClientInstrumentation();

            if (endpoint is not null)
            {
                tracing.AddOtlpExporter(options => options.Endpoint = endpoint);
            }
        });

        openTelemetry.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (endpoint is not null)
            {
                metrics.AddOtlpExporter(options => options.Endpoint = endpoint);
            }
        });

        return services;
    }

    private static Uri? GetOtlpEndpoint(IConfiguration configuration)
    {
        var configuredEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? configuration["Observability:OtlpEndpoint"];

        if (string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            return null;
        }

        if (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "OTEL_EXPORTER_OTLP_ENDPOINT must be an absolute HTTP(S) URI.");
        }

        return endpoint;
    }

    private static double GetTraceSampleRatio(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredRatio = configuration["OTEL_TRACES_SAMPLER_ARG"]
            ?? configuration["Observability:TraceSampleRatio"];

        if (string.IsNullOrWhiteSpace(configuredRatio))
        {
            return environment.IsDevelopment() ? 1d : 0.1d;
        }

        if (!double.TryParse(
                configuredRatio,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var ratio)
            || !double.IsFinite(ratio)
            || ratio is < 0d or > 1d)
        {
            throw new InvalidOperationException(
                "OTEL_TRACES_SAMPLER_ARG must be a number between 0 and 1.");
        }

        return ratio;
    }
}
