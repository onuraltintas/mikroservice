using EduPlatform.Shared.Infrastructure.Logging;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Identity.API.IntegrationTests;

// Roadmap P4: while a service is starting, /health/ready fails until RabbitMQ and the
// database are reachable. Those first readiness errors are expected, so they must not
// fill the error log; after the startup window every health-check error is kept.
public class StartupHealthCheckNoiseTests
{
    private const string HealthChecks = "Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService";

    private static LogEvent Event(LogEventLevel level, string source) => new(
        DateTimeOffset.UtcNow,
        level,
        null,
        new MessageTemplateParser().Parse("Health check {HealthCheckName} completed with status Unhealthy"),
        [new LogEventProperty("SourceContext", new ScalarValue(source))]);

    [Fact]
    public void HealthCheckErrorsAreDroppedOnlyDuringStartup()
    {
        StartupHealthCheckNoise.ShouldDrop(Event(LogEventLevel.Error, HealthChecks), TimeSpan.FromSeconds(20))
            .Should().BeTrue("the bus is still connecting");
        StartupHealthCheckNoise.ShouldDrop(Event(LogEventLevel.Warning, HealthChecks), TimeSpan.FromSeconds(20))
            .Should().BeTrue("a degraded check during startup is expected too");

        StartupHealthCheckNoise.ShouldDrop(Event(LogEventLevel.Error, HealthChecks), TimeSpan.FromSeconds(120))
            .Should().BeFalse("after startup a failing check is a real fault");
        StartupHealthCheckNoise.ShouldDrop(Event(LogEventLevel.Error, "Identity.API.Controllers.AuthController"), TimeSpan.FromSeconds(20))
            .Should().BeFalse("only health-check noise is filtered");
        StartupHealthCheckNoise.ShouldDrop(Event(LogEventLevel.Fatal, HealthChecks), TimeSpan.FromSeconds(20))
            .Should().BeFalse("fatal events are never hidden");
    }

    [Fact]
    public void TheDefaultCheckUsesProcessUptime()
    {
        StartupHealthCheckNoise.Window.Should().Be(TimeSpan.FromSeconds(90));
        StartupHealthCheckNoise.ShouldDrop(Event(LogEventLevel.Error, HealthChecks)).Should().Be(
            StartupHealthCheckNoise.Uptime < StartupHealthCheckNoise.Window);
    }

    private static HttpContext Request(string path, int status)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.StatusCode = status;
        return context;
    }

    [Fact]
    public void ReadinessFailuresDuringStartupAreLoggedBelowTheMinimumLevel()
    {
        StartupHealthCheckNoise.RequestLevel(Request("/health/ready", 503), null, TimeSpan.FromSeconds(20))
            .Should().Be(LogEventLevel.Debug, "the request log must not print ERR while dependencies connect");
        StartupHealthCheckNoise.RequestLevel(Request("/health/ready", 503), null, TimeSpan.FromSeconds(120))
            .Should().Be(LogEventLevel.Error, "an unready service after startup is a fault");
        StartupHealthCheckNoise.RequestLevel(Request("/api/auth/login", 503), null, TimeSpan.FromSeconds(20))
            .Should().Be(LogEventLevel.Error, "other endpoints keep the normal level");
        StartupHealthCheckNoise.RequestLevel(Request("/health/ready", 200), null, TimeSpan.FromSeconds(20))
            .Should().Be(LogEventLevel.Information);
        StartupHealthCheckNoise.RequestLevel(Request("/health/ready", 503), new InvalidOperationException(), TimeSpan.FromSeconds(20))
            .Should().Be(LogEventLevel.Error, "an exception is never hidden");
    }
}
