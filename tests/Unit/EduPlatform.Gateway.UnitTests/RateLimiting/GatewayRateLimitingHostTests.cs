using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EduPlatform.Gateway.UnitTests.Infrastructure;

namespace EduPlatform.Gateway.UnitTests.RateLimiting;

/// <summary>
/// End-to-end limits through the real pipeline. Each test isolates its Redis keys.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class GatewayRateLimitingHostTests
{
    private readonly BackendFixture _fixture;

    public GatewayRateLimitingHostTests(BackendFixture fixture) => _fixture = fixture;

    public static TheoryData<string> RedisModes() => RedisTestServer.IsAvailable
        ? new TheoryData<string> { "redis", "fallback" }
        : new TheoryData<string> { "fallback" };

    [Theory]
    [MemberData(nameof(RedisModes))]
    public async Task A_classroom_behind_one_school_ip_can_refresh_tokens_together(string mode)
    {
        await using var gateway = _fixture.CreateGateway(Settings(mode));
        using var client = gateway.CreateClient();
        var schoolIp = GatewayFactory.RandomClientIp();

        var statuses = new List<HttpStatusCode>();
        for (var student = 0; student < 40; student++)
        {
            using var request = Post("/api/auth/refresh-token", schoolIp);
            using var response = await client.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        statuses.Should().OnlyContain(status => status == HttpStatusCode.OK,
            "the previous 30/min per-IP auth limit rejected the 31st student");
    }

    [Theory]
    [MemberData(nameof(RedisModes))]
    public async Task Login_attempts_from_one_address_are_limited_to_60_per_minute(string mode)
    {
        await using var gateway = _fixture.CreateGateway(Settings(mode));
        using var client = gateway.CreateClient();
        var clientIp = GatewayFactory.RandomClientIp();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 61; attempt++)
        {
            last?.Dispose();
            last = await client.SendAsync(Post("/api/auth/login", clientIp));
            if (attempt < 60)
            {
                last.StatusCode.Should().Be(HttpStatusCode.OK, $"attempt {attempt + 1} is within the limit");
            }
        }

        using var rejected = last!;
        await GatewayEdgeBehaviourTests.AssertRateLimitProblemAsync(rejected);
    }

    [Theory]
    [MemberData(nameof(RedisModes))]
    public async Task Authenticated_traffic_is_limited_per_user_not_per_address(string mode)
    {
        var settings = Settings(mode);
        settings["Gateway:RateLimiting:AuthenticatedUser:PermitLimit"] = "3";
        await using var gateway = _fixture.CreateGateway(settings);
        using var client = gateway.CreateClient();
        var sharedIp = GatewayFactory.RandomClientIp();
        var busyUser = GatewayFactory.CreateToken(Guid.NewGuid().ToString());
        var otherUser = GatewayFactory.CreateToken(Guid.NewGuid().ToString());

        var busy = new List<HttpStatusCode>();
        for (var request = 0; request < 4; request++)
        {
            using var response = await client.SendAsync(Get("/api/users/me", sharedIp, busyUser));
            busy.Add(response.StatusCode);
        }

        using var other = await client.SendAsync(Get("/api/users/me", sharedIp, otherUser));

        busy.Should().Equal(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests);
        other.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Notification_hub_is_not_counted_against_the_user_budget()
    {
        var settings = Settings("fallback");
        settings["Gateway:RateLimiting:AuthenticatedUser:PermitLimit"] = "1";
        await using var gateway = _fixture.CreateGateway(settings);
        using var client = gateway.CreateClient();
        var token = GatewayFactory.CreateToken(Guid.NewGuid().ToString());

        using var first = await client.SendAsync(Get("/hubs/notifications/negotiate", null, token));
        using var second = await client.SendAsync(Get("/hubs/notifications/negotiate", null, token));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(RedisModes))]
    public async Task Public_newsletter_writes_stay_limited_in_every_mode(string mode)
    {
        await using var gateway = _fixture.CreateGateway(Settings(mode));
        using var client = gateway.CreateClient();
        var clientIp = GatewayFactory.RandomClientIp();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 9; attempt++)
        {
            using var response = await client.SendAsync(Post("/api/coaching/cms/newsletter/subscriptions", clientIp));
            statuses.Add(response.StatusCode);
        }

        statuses.Take(8).Should().OnlyContain(status => status == HttpStatusCode.OK);
        statuses[8].Should().Be(HttpStatusCode.TooManyRequests, "Redis outages previously disabled this limit");
    }

    [Theory]
    [MemberData(nameof(RedisModes))]
    public async Task Limited_routes_advertise_their_remaining_budget(string mode)
    {
        await using var gateway = _fixture.CreateGateway(Settings(mode));
        using var client = gateway.CreateClient();

        using var response = await client.SendAsync(Post("/api/support/submit", GatewayFactory.RandomClientIp()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("RateLimit-Limit").Should().ContainSingle("10");
        response.Headers.GetValues("RateLimit-Remaining").Should().ContainSingle("9");
        int.Parse(response.Headers.GetValues("RateLimit-Reset").Single()).Should().BeInRange(1, 60);
    }

    [Fact]
    public async Task Rate_limiting_can_be_switched_off_for_controlled_load_tests()
    {
        var settings = Settings("fallback");
        settings["Gateway:RateLimiting:Enabled"] = "false";
        await using var gateway = _fixture.CreateGateway(settings);
        using var client = gateway.CreateClient();
        var clientIp = GatewayFactory.RandomClientIp();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 12; attempt++)
        {
            using var response = await client.SendAsync(Post("/api/support/submit", clientIp));
            statuses.Add(response.StatusCode);
        }

        statuses.Should().OnlyContain(status => status == HttpStatusCode.OK);
    }

    private static Dictionary<string, string?> Settings(string mode)
    {
        var settings = mode == "redis"
            ? RedisTestServer.GatewaySettings()
            : GatewayEdgeBehaviourTests.UnreachableRedisSettings();
        settings["Gateway:RateLimiting:KeyPrefix"] = $"test:{Guid.NewGuid():N}";
        return settings;
    }

    private static HttpRequestMessage Post(string path, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { value = "x" })
        };
        request.Headers.Add(GatewayFactory.TestRemoteIpHeader, clientIp);
        return request;
    }

    private static HttpRequestMessage Get(string path, string? clientIp, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (clientIp is not null)
        {
            request.Headers.Add(GatewayFactory.TestRemoteIpHeader, clientIp);
        }

        return request;
    }
}
