using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Identity.Application.DTOs.Settings;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using EduPlatform.Shared.Security.Authorization;
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

    [Fact]
    public async Task MfaConfiguration_ShouldAcceptOnlyPrivateKnownModes()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var invalidMode = () => service.CreateConfigurationAsync(
            new CreateConfigurationRequest
            {
                Key = MfaOperationCategories.ConfigurationKey(MfaOperationCategories.Users),
                Value = "sometimes",
                Description = "MFA",
                DataType = ConfigurationDataType.String,
                Group = "Security",
                IsPublic = false
            },
            CancellationToken.None);
        var publicMode = () => service.CreateConfigurationAsync(
            new CreateConfigurationRequest
            {
                Key = MfaOperationCategories.ConfigurationKey(MfaOperationCategories.Users),
                Value = MfaPolicyModes.Disabled,
                Description = "MFA",
                DataType = ConfigurationDataType.String,
                Group = "Security",
                IsPublic = true
            },
            CancellationToken.None);

        await invalidMode.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*required, mutations veya disabled*");
        await publicMode.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*herkese açık olamaz*");
    }

    [Fact]
    public async Task MfaConfiguration_ShouldNotBeDeleted()
    {
        await using var context = CreateContext();
        context.Configurations.Add(SystemConfiguration.Create(
            MfaOperationCategories.ConfigurationKey(MfaOperationCategories.Users),
            MfaPolicyModes.MutationsOnly,
            "MFA",
            ConfigurationDataType.String,
            "Security"));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var delete = () => service.DeleteConfigurationAsync(
            MfaOperationCategories.ConfigurationKey(MfaOperationCategories.Users),
            CancellationToken.None);

        await delete.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*silinemez*");
    }

    [Fact]
    public async Task RefreshCache_ShouldKeepMfaPolicyWithoutExpiration()
    {
        await using var context = CreateContext();
        context.Configurations.Add(SystemConfiguration.Create(
            MfaOperationCategories.ConfigurationKey(MfaOperationCategories.Users),
            MfaPolicyModes.Disabled,
            "MFA",
            ConfigurationDataType.String,
            "Security"));
        await context.SaveChangesAsync();
        var cache = new RecordingCache();
        var service = new ConfigurationService(context, cache, NullLogger<ConfigurationService>.Instance);

        await service.RefreshCacheAsync(CancellationToken.None);

        cache.MfaEntryOptions.Should().NotBeNull();
        cache.MfaEntryOptions!.AbsoluteExpirationRelativeToNow.Should().BeNull();
        cache.MfaEntryOptions.AbsoluteExpiration.Should().BeNull();
        cache.MfaEntryOptions.SlidingExpiration.Should().BeNull();
        cache.RemovedMfaKey.Should().BeTrue();
    }

    [Fact]
    public async Task MfaPolicyRead_ShouldNotPopulateCache()
    {
        await using var context = CreateContext();
        context.Configurations.Add(SystemConfiguration.Create(
            MfaOperationCategories.ConfigurationKey(MfaOperationCategories.System),
            MfaPolicyModes.Disabled,
            "MFA",
            ConfigurationDataType.String,
            "Security"));
        await context.SaveChangesAsync();
        var cache = new RecordingCache();
        var service = new ConfigurationService(context, cache, NullLogger<ConfigurationService>.Instance);

        var value = await service.GetConfigurationValueAsync(
            MfaOperationCategories.ConfigurationKey(MfaOperationCategories.System),
            CancellationToken.None);

        value.Should().Be(MfaPolicyModes.Disabled);
        cache.MfaEntryOptions.Should().BeNull();
    }

    [Fact]
    public async Task MfaPolicyRead_ShouldPreferDatabaseOverStaleCache()
    {
        await using var context = CreateContext();
        var key = MfaOperationCategories.ConfigurationKey(MfaOperationCategories.System);
        context.Configurations.Add(SystemConfiguration.Create(
            key,
            MfaPolicyModes.Disabled,
            "MFA",
            ConfigurationDataType.String,
            "Security"));
        await context.SaveChangesAsync();
        IDistributedCache cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        await cache.SetStringAsync($"config:{key}", MfaPolicyModes.Required);
        var service = new ConfigurationService(context, cache, NullLogger<ConfigurationService>.Instance);

        var mode = await service.GetConfigurationValueAsync(key, CancellationToken.None);

        mode.Should().Be(MfaPolicyModes.Disabled);
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

    private sealed class RecordingCache : IDistributedCache
    {
        public DistributedCacheEntryOptions? MfaEntryOptions { get; private set; }
        public bool RemovedMfaKey { get; private set; }

        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => RecordRemoval(key);
        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            RecordRemoval(key);
            return Task.CompletedTask;
        }
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Record(key, options);
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Record(key, options);
            return Task.CompletedTask;
        }

        private void Record(string key, DistributedCacheEntryOptions options)
        {
            if (key.StartsWith("config:security.mfa.", StringComparison.Ordinal))
            {
                MfaEntryOptions = options;
            }
        }

        private void RecordRemoval(string key)
        {
            if (key.StartsWith("config:security.mfa.", StringComparison.Ordinal))
            {
                RemovedMfaKey = true;
            }
        }
    }
}
