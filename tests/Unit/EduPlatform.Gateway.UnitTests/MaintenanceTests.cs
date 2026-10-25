using System.Net;
using System.Net.Http.Headers;
using EduPlatform.Gateway.Configuration;
using EduPlatform.Gateway.Middlewares;
using EduPlatform.Gateway.UnitTests.Infrastructure;
using EduPlatform.Gateway.UnitTests.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EduPlatform.Gateway.UnitTests;

public sealed class MaintenanceFlagStoreTests
{
    [Fact]
    public async Task Flags_are_read_from_redis_at_most_once_per_cache_period()
    {
        var cache = new CountingCache { ["config:system.maintenancemode"] = "true" };
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-25T10:00:00Z"));
        var store = CreateStore(cache, clock, cacheSeconds: 5);

        (await store.IsEnabledAsync("system.maintenancemode", CancellationToken.None)).Should().BeTrue();
        (await store.IsEnabledAsync("system.maintenancemode", CancellationToken.None)).Should().BeTrue();
        cache.Reads.Should().Be(1);

        cache["config:system.maintenancemode"] = "false";
        clock.Advance(TimeSpan.FromSeconds(6));

        (await store.IsEnabledAsync("system.maintenancemode", CancellationToken.None)).Should().BeFalse();
        cache.Reads.Should().Be(2);
    }

    [Fact]
    public async Task A_redis_failure_keeps_the_last_known_state_instead_of_reopening_the_service()
    {
        var cache = new CountingCache { ["config:maintenance.coaching"] = "true" };
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-25T10:00:00Z"));
        var store = CreateStore(cache, clock, cacheSeconds: 5);
        (await store.IsEnabledAsync("maintenance.coaching", CancellationToken.None)).Should().BeTrue();

        cache.Fail = true;
        clock.Advance(TimeSpan.FromSeconds(6));

        (await store.IsEnabledAsync("maintenance.coaching", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task A_redis_failure_without_a_known_state_leaves_the_service_open()
    {
        var cache = new CountingCache { Fail = true };
        var store = CreateStore(cache, TimeProvider.System, cacheSeconds: 5);

        (await store.IsEnabledAsync("maintenance.identity", CancellationToken.None)).Should().BeFalse();
    }

    private static MaintenanceFlagStore CreateStore(IDistributedCache cache, TimeProvider clock, int cacheSeconds) =>
        new(cache, clock, Options.Create(new GatewayMaintenanceOptions { CacheSeconds = cacheSeconds }),
            NullLogger<MaintenanceFlagStore>.Instance);

    private sealed class CountingCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _values = new();

        public int Reads { get; private set; }

        public bool Fail { get; set; }

        public string this[string key]
        {
            set => _values[key] = System.Text.Encoding.UTF8.GetBytes(value);
        }

        public byte[]? Get(string key) => throw new NotSupportedException();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Reads++;
            if (Fail)
            {
                throw new StackExchange.Redis.RedisConnectionException(
                    StackExchange.Redis.ConnectionFailureType.UnableToConnect, "down");
            }

            return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new NotSupportedException();

        public void Refresh(string key) => throw new NotSupportedException();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();

        public void Remove(string key) => throw new NotSupportedException();

        public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
    }
}

[Collection(GatewayCollection.Name)]
public sealed class MaintenanceHostTests
{
    private readonly BackendFixture _fixture;

    public MaintenanceHostTests(BackendFixture fixture) => _fixture = fixture;

    [RedisTheory]
    [InlineData("/api/auth/login", HttpStatusCode.OK)]
    [InlineData("/api/configurations/public/maintenance", HttpStatusCode.OK)]
    [InlineData("/api/coaching/refresh-token-report", HttpStatusCode.ServiceUnavailable)]
    [InlineData("/api/auth/staff-session/products", HttpStatusCode.OK)]
    [InlineData("/api/users/me", HttpStatusCode.ServiceUnavailable)]
    public async Task Global_maintenance_only_lets_sign_in_and_public_configuration_through(string path, HttpStatusCode expected)
    {
        await using var scope = await GlobalMaintenanceAsync();
        await using var gateway = _fixture.CreateGateway(Settings());
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["Student"]));

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(expected);
    }

    [RedisFact]
    public async Task Bypass_roles_come_from_configuration()
    {
        await using var scope = await GlobalMaintenanceAsync();
        var settings = Settings();
        settings["Gateway:Maintenance:BypassRoles:0"] = "SystemAdmin";
        settings["Gateway:Maintenance:BypassRoles:1"] = "SystemAdmin";
        await using var gateway = _fixture.CreateGateway(settings);
        using var client = gateway.CreateClient();

        using var owner = await client.SendAsync(Get("InstitutionOwner"));
        using var admin = await client.SendAsync(Get("SystemAdmin"));

        owner.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        admin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RedisFact]
    public async Task Institution_owners_keep_their_default_bypass()
    {
        await using var scope = await GlobalMaintenanceAsync();
        await using var gateway = _fixture.CreateGateway(Settings());
        using var client = gateway.CreateClient();

        using var owner = await client.SendAsync(Get("InstitutionOwner"));

        owner.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static HttpRequestMessage Get(string role)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: [role]));
        return request;
    }

    private static Dictionary<string, string?> Settings()
    {
        var settings = RedisTestServer.GatewaySettings();
        settings["Gateway:RateLimiting:Enabled"] = "false";
        return settings;
    }

    private static async Task<IAsyncDisposable> GlobalMaintenanceAsync()
    {
        var cache = RedisTestServer.CreateSharedCache();
        await cache.SetStringAsync("config:system.maintenancemode", "true");
        return new Cleanup(cache);
    }

    private sealed class Cleanup(Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache cache) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await cache.RemoveAsync("config:system.maintenancemode");
            cache.Dispose();
        }
    }
}
