using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// The gateway reads maintenance switches straight from the shared Redis cache. If the
/// cached flag expired, the gateway would treat a service that is still in maintenance as
/// open, so maintenance keys are cached without expiry and kept current on every write.
/// </summary>
public sealed class MaintenanceConfigurationCacheTests
{
    [Theory]
    [InlineData("system.maintenancemode")]
    [InlineData("maintenance.coaching")]
    [InlineData("maintenance.speed-reading")]
    public async Task Maintenance_flags_are_cached_without_expiry(string key)
    {
        await using var context = CreateContext();
        context.Configurations.Add(SystemConfiguration.Create(key, "true", "Maintenance", ConfigurationDataType.Boolean, "System"));
        await context.SaveChangesAsync();
        var cache = new RecordingCache();
        var service = new ConfigurationService(context, cache, NullLogger<ConfigurationService>.Instance);

        await service.RefreshCacheAsync(CancellationToken.None);

        cache.Writes.Should().ContainKey($"config:{key}");
        var options = cache.Writes[$"config:{key}"];
        options.AbsoluteExpiration.Should().BeNull();
        options.AbsoluteExpirationRelativeToNow.Should().BeNull();
        options.SlidingExpiration.Should().BeNull();
    }

    [Fact]
    public async Task Other_configuration_keeps_the_24_hour_cache_lifetime()
    {
        await using var context = CreateContext();
        context.Configurations.Add(SystemConfiguration.Create("ui.theme", "light", "Theme", ConfigurationDataType.String, "UI"));
        await context.SaveChangesAsync();
        var cache = new RecordingCache();
        var service = new ConfigurationService(context, cache, NullLogger<ConfigurationService>.Instance);

        await service.RefreshCacheAsync(CancellationToken.None);

        cache.Writes["config:ui.theme"].AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromHours(24));
    }

    private static IdentityDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class RecordingCache : IDistributedCache
    {
        public Dictionary<string, DistributedCacheEntryOptions> Writes { get; } = new();

        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Writes[key] = options;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Writes[key] = options;
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }
}
