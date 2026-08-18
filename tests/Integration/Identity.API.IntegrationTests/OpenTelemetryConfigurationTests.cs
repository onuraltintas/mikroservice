using EduPlatform.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Identity.API.IntegrationTests;

public sealed class OpenTelemetryConfigurationTests
{
    [Fact]
    public void InvalidOtlpEndpoint_ShouldFailFast()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "ftp://collector:4317"
            })
            .Build();

        var services = new ServiceCollection();

        var action = () => services.AddEduPlatformOpenTelemetry(
            configuration,
            new TestHostEnvironment { EnvironmentName = Environments.Production },
            "EduPlatform.Tests");

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*OTEL_EXPORTER_OTLP_ENDPOINT*");
    }

    [Fact]
    public void MissingOtlpEndpoint_ShouldKeepExporterOptIn()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var action = () => services.AddEduPlatformOpenTelemetry(
            configuration,
            new TestHostEnvironment { EnvironmentName = Environments.Development },
            "EduPlatform.Tests");

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void NonFiniteTraceSampleRatio_ShouldFailFast(string ratio)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_TRACES_SAMPLER_ARG"] = ratio
            })
            .Build();

        var services = new ServiceCollection();

        var action = () => services.AddEduPlatformOpenTelemetry(
            configuration,
            new TestHostEnvironment { EnvironmentName = Environments.Production },
            "EduPlatform.Tests");

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*OTEL_TRACES_SAMPLER_ARG*");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "EduPlatform.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
