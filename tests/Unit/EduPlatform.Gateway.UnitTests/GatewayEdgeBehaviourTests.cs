using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EduPlatform.Gateway.UnitTests.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;

namespace EduPlatform.Gateway.UnitTests;

/// <summary>
/// Error format, CORS, correlation and Redis configuration behaviour at the gateway edge.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class GatewayEdgeBehaviourTests
{
    private const string OperationsViewPermission = "Permissions.Operations.View";
    private readonly BackendFixture _fixture;

    public GatewayEdgeBehaviourTests(BackendFixture fixture) => _fixture = fixture;

    [RedisFact]
    public async Task Rate_limited_response_is_problem_json_and_keeps_the_legacy_error_field()
    {
        await using var gateway = _fixture.CreateGateway(RedisTestServer.GatewaySettings());
        using var client = gateway.CreateClient();
        var clientIp = GatewayFactory.RandomClientIp();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            last?.Dispose();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password")
            {
                Content = JsonContent.Create(new { email = "student@example.com" })
            };
            request.Headers.Add(GatewayFactory.TestRemoteIpHeader, clientIp);
            last = await client.SendAsync(request);
        }

        using var rejected = last!;
        await AssertRateLimitProblemAsync(rejected);
    }

    [Fact]
    public async Task Rate_limit_still_applies_with_problem_json_when_redis_is_unreachable()
    {
        await using var gateway = _fixture.CreateGateway(UnreachableRedisSettings());
        using var client = gateway.CreateClient();
        var clientIp = GatewayFactory.RandomClientIp();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            last?.Dispose();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/support/submit")
            {
                Content = JsonContent.Create(new { message = "help" })
            };
            request.Headers.Add(GatewayFactory.TestRemoteIpHeader, clientIp);
            last = await client.SendAsync(request);
        }

        using var rejected = last!;
        await AssertRateLimitProblemAsync(rejected);
    }

    [Fact]
    public async Task Non_rate_limited_routes_work_quickly_when_redis_is_unreachable()
    {
        await using var gateway = _fixture.CreateGateway(UnreachableRedisSettings());
        using var client = gateway.CreateClient();
        var started = System.Diagnostics.Stopwatch.StartNew();

        using var response = await client.GetAsync("/api/speed-reading/capabilities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [RedisFact]
    public async Task Maintenance_response_is_problem_json_with_legacy_fields()
    {
        await using var gateway = _fixture.CreateGateway(RedisTestServer.GatewaySettings());
        using var client = gateway.CreateClient();
        using var cache = RedisTestServer.CreateSharedCache();
        await cache.SetStringAsync("config:maintenance.notification", "true");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/notifications");
            request.Headers.Authorization = Bearer(GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["Student"]));

            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("status").GetInt32().Should().Be(503);
            body.RootElement.GetProperty("type").GetString().Should().Be("https://eduplatform.dev/problems/service-unavailable");
            body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
            body.RootElement.GetProperty("error").GetString().Should().Be("Service Unavailable");
            body.RootElement.GetProperty("message").GetString().Should().Be(body.RootElement.GetProperty("detail").GetString());
            body.RootElement.GetProperty("service").GetString().Should().Be("notification");
        }
        finally
        {
            await cache.RemoveAsync("config:maintenance.notification");
        }
    }

    [Fact]
    public async Task Cors_is_disabled_unless_origins_are_configured()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();

        using var response = await client.SendAsync(Preflight("http://localhost:4200"));

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_allows_only_configured_origins_with_credentials()
    {
        await using var gateway = _fixture.CreateGateway(new Dictionary<string, string?>
        {
            ["Gateway:Cors:AllowedOrigins:0"] = "https://app.example.test"
        });
        using var client = gateway.CreateClient();

        using var allowed = await client.SendAsync(Preflight("https://app.example.test"));
        using var denied = await client.SendAsync(Preflight("https://evil.example.test"));

        allowed.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle("https://app.example.test");
        allowed.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle("true");
        denied.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("not-a-url")]
    [InlineData("https://app.example.test/path")]
    public void Cors_rejects_unsafe_origin_configuration_at_startup(string origin)
    {
        using var gateway = _fixture.CreateGateway(new Dictionary<string, string?>
        {
            ["Gateway:Cors:AllowedOrigins:0"] = origin
        });

        var start = () => gateway.CreateClient();

        start.Should().Throw<Exception>().Where(exception => exception.ToString().Contains("Gateway:Cors:AllowedOrigins"));
    }

    [Fact]
    public async Task Correlation_id_from_the_caller_is_forwarded_to_the_service()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/speed-reading/capabilities");
        request.Headers.Add("X-Correlation-ID", "support-ticket-42");

        using var response = await client.SendAsync(request);

        var echo = await EchoResponse.ReadAsync(response);
        echo.Headers["X-Correlation-ID"].Should().Be("support-ticket-42");
        response.Headers.GetValues("X-Correlation-ID").Should().ContainSingle("support-ticket-42");
    }

    [Fact]
    public async Task Gateway_generated_correlation_id_is_shared_with_the_service()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();

        using var response = await client.GetAsync("/api/speed-reading/capabilities");

        var echo = await EchoResponse.ReadAsync(response);
        var gatewayId = response.Headers.GetValues("X-Correlation-ID").Single();
        echo.Headers["X-Correlation-ID"].Should().Be(gatewayId);
    }

    [Fact]
    public async Task Unsafe_correlation_id_is_replaced_before_forwarding()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/speed-reading/capabilities");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "<script>alert(1)</script>");

        using var response = await client.SendAsync(request);

        var echo = await EchoResponse.ReadAsync(response);
        echo.Headers["X-Correlation-ID"].Should().NotContain("<");
        echo.Headers["X-Correlation-ID"].Should().Be(response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Gateway_service_list_is_forbidden_for_regular_users()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gateway/services");
        request.Headers.Authorization = Bearer(GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["Student"]));

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gateway_service_list_is_available_with_the_operations_view_permission()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gateway/services");
        request.Headers.Authorization = Bearer(GatewayFactory.CreateToken(
            Guid.NewGuid().ToString(),
            permissions: [OperationsViewPermission]));

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    internal static Dictionary<string, string?> UnreachableRedisSettings() => new()
    {
        // Port 1 on loopback refuses immediately: deterministic "Redis down" without waiting.
        ["REDIS_HOST"] = "127.0.0.1",
        ["REDIS_PORT"] = "1",
        ["REDIS_PASSWORD"] = string.Empty
    };

    internal static async Task AssertRateLimitProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        response.Headers.RetryAfter.Should().NotBeNull();
        var retryAfter = response.Headers.RetryAfter!.Delta;
        retryAfter.Should().NotBeNull();
        retryAfter!.Value.Should().BeGreaterThan(TimeSpan.Zero);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetInt32().Should().Be(429);
        body.RootElement.GetProperty("type").GetString().Should().Be("https://eduplatform.dev/problems/rate-limit-exceeded");
        body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("error").GetString().Should().Be("Rate limit exceeded.");
        body.RootElement.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/users/me");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return request;
    }
}
