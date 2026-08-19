using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Identity.Application.DTOs.Settings;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

public sealed class ConfigurationSecretSecurityTests
{
    [Fact]
    public async Task SecretConfigurations_ShouldNotBeReturnedByManagementQueries()
    {
        await using var context = CreateContext();
        context.Configurations.Add(SystemConfiguration.Create(
            "smtp.password",
            "do-not-expose",
            "Legacy secret",
            ConfigurationDataType.Secret));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var configurations = await service.GetAllConfigurationsAsync(CancellationToken.None);
        var value = await service.GetManageableConfigurationValueAsync(
            "smtp.password",
            CancellationToken.None);

        configurations.Should().BeEmpty();
        value.Should().BeNull();
    }

    [Fact]
    public async Task PublicQuery_ShouldNotReturnLegacySecretConfiguration()
    {
        await using var context = CreateContext();
        context.Configurations.Add(SystemConfiguration.Create(
            "smtp.password",
            "do-not-expose",
            "Legacy public secret",
            ConfigurationDataType.Secret,
            isPublic: true));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var value = await service.GetPublicConfigurationValueAsync(
            "smtp.password",
            CancellationToken.None);

        value.Should().BeNull();
    }

    [Fact]
    public async Task CreateSecretConfiguration_ShouldBeRejected()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var request = new CreateConfigurationRequest
        {
            Key = "smtp.password",
            Value = "do-not-store",
            Description = "Secret",
            DataType = ConfigurationDataType.Secret,
            Group = "Mail",
            IsPublic = false
        };

        var action = () => service.CreateConfigurationAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*secret manager*");
    }

    [Fact]
    public async Task ExistingSecretConfiguration_ShouldNotBeMutableFromManagementApi()
    {
        await using var context = CreateContext();
        context.Configurations.Add(SystemConfiguration.Create(
            "smtp.password",
            "legacy-secret",
            "Legacy secret",
            ConfigurationDataType.Secret));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var update = () => service.UpdateConfigurationAsync(
            "smtp.password",
            new UpdateConfigurationRequest { Value = "replacement" },
            CancellationToken.None);
        var delete = () => service.DeleteConfigurationAsync(
            "smtp.password",
            CancellationToken.None);

        await update.Should().ThrowAsync<BusinessRuleException>();
        await delete.Should().ThrowAsync<BusinessRuleException>();
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private static ConfigurationService CreateService(IdentityDbContext context)
    {
        IDistributedCache cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        return new ConfigurationService(
            context,
            cache,
            NullLogger<ConfigurationService>.Instance);
    }
}
