using EduPlatform.Shared.Infrastructure.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class DataProtectionConfigurationTests
{
    [Fact]
    public void ProductionWithoutCertificate_ShouldFailClosed()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), $"eduplatform-dp-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["DataProtection:KeysPath"] = keyPath
            })
            .Build();

        try
        {
            var services = new ServiceCollection();

            var action = () => services.AddPersistentDataProtection(configuration, "EduPlatform.Test");

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*DataProtection:CertificatePath*Production*");
        }
        finally
        {
            if (Directory.Exists(keyPath))
            {
                Directory.Delete(keyPath, recursive: true);
            }
        }
    }

    [Fact]
    public void ProductionWithoutSharedKeyPath_ShouldFailClosed()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production"
            })
            .Build();

        var services = new ServiceCollection();

        var action = () => services.AddPersistentDataProtection(configuration, "EduPlatform.Test");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*DataProtection:KeysPath*shared*persistent key ring*");
    }

    [Fact]
    public void EmptyAspNetCoreEnvironment_ShouldFallBackToProductionEnvironment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = string.Empty,
                ["ENVIRONMENT"] = "Production"
            })
            .Build();

        var services = new ServiceCollection();

        var action = () => services.AddPersistentDataProtection(configuration, "EduPlatform.Test");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*DataProtection:KeysPath*shared*persistent key ring*");
    }
}
