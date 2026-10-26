using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Notification.Infrastructure.Services;

namespace Identity.API.IntegrationTests;

public sealed class NotificationOperationalTests
{
    [Fact]
    public void NotificationImage_RunsAsTheNonRootUser_AndDeploymentPreparesIt()
    {
        var dockerfile = Read("services", "notification-service", "Dockerfile");
        var compose = Read("docker-compose.yml");
        var production = Read("docker-compose.production.yml");

        dockerfile.Should().Contain("USER $APP_UID").And.NotContain("dotnet build");
        compose.Should().Contain("notification-volume-permissions:");
        var notification = production[production.IndexOf("\n  notification-service:", StringComparison.Ordinal)..];
        notification[..notification.IndexOf("restart:", StringComparison.Ordinal)]
            .Should().Contain("runtime-secrets-preflight:", "notification also reads the PFX as UID 1654");
    }

    [Fact]
    public void Program_LoadsDotEnvOnlyInDevelopment_AndHasNoRedisDependency()
    {
        var program = Read("services", "notification-service", "Notification.API", "Program.cs");

        program.Should().Contain("Env.NoClobber().Load(envPath)");
        program.Should().Contain("string.Equals(startupEnvironment, \"Development\"");
        program.Should().NotContain("AddRedisCache", "nothing in the service uses Redis");
    }

    [Theory]
    [InlineData("587", true)]
    [InlineData("", true)]
    [InlineData("smtp", false)]
    [InlineData("70000", false)]
    public void SmtpPort_IsValidatedAtStartup(string port, bool valid)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Email:Port"] = port })
            .Build();

        var validate = () => EmailService.ValidateConfiguration(configuration);

        if (valid) validate.Should().NotThrow();
        else validate.Should().Throw<InvalidOperationException>();
    }

    private static string Read(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EduPlatform.sln"))) directory = directory.Parent;
        return File.ReadAllText(Path.Combine([directory!.FullName, .. segments]));
    }
}
