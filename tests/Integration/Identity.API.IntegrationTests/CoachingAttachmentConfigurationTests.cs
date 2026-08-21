using Coaching.Application.Attachments;
using Coaching.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAttachmentConfigurationTests
{
    [Fact]
    public void Production_RejectsLocalStorageProvider()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Coaching:Attachments:Provider"] = "Local",
            ["Coaching:Attachments:Scanner:Provider"] = "ClamAv"
        });

        var services = new ServiceCollection();
        var act = () => services.AddInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Provider=Minio*");
    }

    [Fact]
    public void Production_RejectsLocalScannerProvider()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Coaching:Attachments:Provider"] = "Minio",
            ["Coaching:Attachments:MinioEndpoint"] = "minio:9000",
            ["Coaching:Attachments:MinioAccessKey"] = "access",
            ["Coaching:Attachments:MinioSecretKey"] = "secret",
            ["Coaching:Attachments:MinioBucket"] = "attachments",
            ["Coaching:Attachments:Scanner:Provider"] = "Local"
        });

        var services = new ServiceCollection();
        var act = () => services.AddInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Scanner:Provider=ClamAv*");
    }

    [Fact]
    public void Development_RegistersLocalStorageAndScanner()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["Coaching:Attachments:Provider"] = "Local",
            ["Coaching:Attachments:Scanner:Provider"] = "Local"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        services.Any(descriptor =>
            descriptor.ServiceType == typeof(IAssignmentAttachmentStorage)
            && descriptor.ImplementationType?.Name == "LocalAssignmentAttachmentStorage").Should().BeTrue();
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(IAssignmentAttachmentScanner)
            && descriptor.ImplementationType?.Name == "DevelopmentAttachmentScanner").Should().BeTrue();
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        values.TryAdd("ConnectionStrings:DefaultConnection", "Host=localhost;Database=coaching;Username=test;Password=test");
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
