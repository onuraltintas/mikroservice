using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingApiProductionHardeningTests
{
    [Fact]
    public void CoachingApi_ShouldKeepBrowserCorsAtTheGatewayBoundary()
    {
        var source = ReadCoachingProgram();

        source.Should().NotContain("AllowAnyOrigin");
        source.Should().NotContain("UseCors(");
    }

    [Fact]
    public void CoachingApi_ShouldSeparateLivenessFromDependencyReadiness()
    {
        var source = ReadCoachingProgram();

        source.Should().Contain("Tags = [\"ready\"]");
        source.Should().Contain("Predicate = _ => false");
        source.Should().Contain("Predicate = check => check.Tags.Contains(\"ready\")");
    }

    private static string ReadCoachingProgram() =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "services",
            "coaching-service",
            "Coaching.API",
            "Program.cs"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EduPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
