using FluentAssertions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// The gateway actively probes /health/live on every cluster. Liveness must not depend on
/// PostgreSQL, Redis or RabbitMQ, otherwise a dependency blip would take the service out of
/// rotation; dependency checks stay on /health/ready (and /health).
/// </summary>
public sealed class ServiceLivenessProbeTests
{
    [Theory]
    [InlineData("identity-service", "Identity.API")]
    [InlineData("notification-service", "Notification.API")]
    public void Liveness_runs_no_dependency_checks(string service, string project)
    {
        var source = ReadRepositoryFile("services", service, project, "Program.cs");

        source.Should().Contain("app.MapHealthChecks(\"/health/live\", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();");
        source.Should().Contain("app.MapHealthChecks(\"/health/ready\").AllowAnonymous();");
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EduPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine([directory!.FullName, .. segments]));
    }
}
