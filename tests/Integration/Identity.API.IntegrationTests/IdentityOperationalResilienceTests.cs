using FluentAssertions;
using Identity.API.Security;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Identity.API.IntegrationTests;

public sealed class IdentityOperationalResilienceTests
{
    [Fact]
    public async Task ConfigurationReads_FallBackToTheDatabaseWhenRedisIsDown()
    {
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        context.Configurations.Add(SystemConfiguration.Create("maintenance.identity", "true", "Maintenance", ConfigurationDataType.Boolean));
        context.Configurations.Add(SystemConfiguration.Create("site.title", "EduPlatform", "Title", ConfigurationDataType.String, isPublic: true));
        await context.SaveChangesAsync();
        var service = new ConfigurationService(context, new UnavailableCache(), NullLogger<ConfigurationService>.Instance);

        (await service.GetConfigurationValueAsync("maintenance.identity", CancellationToken.None)).Should().Be("true");
        (await service.GetPublicConfigurationValueAsync("site.title", CancellationToken.None)).Should().Be("EduPlatform");
    }

    [Fact]
    public async Task RedisReadiness_ReportsUnhealthyInsteadOfThrowing()
    {
        var check = new RedisReadinessCheck(new Lazy<IConnectionMultiplexer>(
            () => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down")));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void IdentityImage_RunsAsTheNonRootUser_AndComposePreparesItsVolume()
    {
        var dockerfile = ReadRepositoryFile("services", "identity-service", "Dockerfile");
        var compose = ReadRepositoryFile("docker-compose.yml");
        var production = ReadRepositoryFile("docker-compose.production.yml");

        dockerfile.Should().Contain("USER $APP_UID");
        dockerfile.Should().Contain("chown -R \"$APP_UID\" /var/lib/eduplatform");
        compose.Should().Contain("identity-volume-permissions:");
        compose.Should().Contain("/health/ready");
        production.Should().Contain("runtime-secrets-preflight:");
        production.Should().Contain("user: \"1654:1654\"");
    }

    [Fact]
    public void DotEnvFile_IsLoadedOnlyInDevelopment()
    {
        var program = ReadRepositoryFile("services", "identity-service", "Identity.API", "Program.cs");

        program.Should().Contain("string.Equals(startupEnvironment, \"Development\"");
        program.Should().Contain("Env.NoClobber().Load(envPath)");
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

    private sealed class UnavailableCache : IDistributedCache
    {
        private static Exception Down() => new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis unavailable");

        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();
    }
}
