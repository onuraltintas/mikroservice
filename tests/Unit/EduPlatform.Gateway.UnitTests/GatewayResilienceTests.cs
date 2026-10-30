using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EduPlatform.Gateway.Proxy;
using EduPlatform.Gateway.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;

namespace EduPlatform.Gateway.UnitTests;

/// <summary>Body limits, request timeouts, upstream health and connection handling.</summary>
[Collection(GatewayCollection.Name)]
public sealed class GatewayResilienceTests
{
    private readonly BackendFixture _fixture;

    public GatewayResilienceTests(BackendFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/support/submit")]
    [InlineData("/api/coaching/cms/newsletter/subscriptions")]
    public async Task Small_public_json_endpoints_reject_oversized_bodies_before_forwarding(string path)
    {
        await using var gateway = _fixture.CreateGateway(DisableLimits());
        using var client = gateway.CreateClient();
        var before = _fixture.Backend.Requests.Count;

        using var response = await client.PostAsync(path, new ByteArrayContent(new byte[65_537]));

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "https://eduplatform.dev/problems/payload-too-large");
        _fixture.Backend.Requests.Count.Should().Be(before, "the oversized body must not reach the service");
    }

    [Fact]
    public async Task Requests_without_a_specific_rule_keep_the_previous_30_mb_limit()
    {
        await using var gateway = _fixture.CreateGateway(DisableLimits());
        using var client = gateway.CreateClient();
        using var request = AuthorizedPost("/api/coaching-admin/cms/media", new ByteArrayContent(new byte[30_000_001]));

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Catalog_import_accepts_the_48_mb_that_the_coaching_service_allows()
    {
        await using var gateway = _fixture.CreateGateway(DisableLimits());
        using var client = gateway.CreateClient();
        using var request = AuthorizedPost("/api/coaching-admin/catalog-imports", new ByteArrayContent(new byte[40 * 1024 * 1024]));

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EchoResponse.ReadAsync(response)).BodyLength.Should().Be(40 * 1024 * 1024);
    }

    [Fact]
    public async Task Kestrel_accepts_bodies_up_to_the_largest_configured_rule()
    {
        await using var gateway = _fixture.CreateGateway(DisableLimits());
        using var _ = gateway.CreateClient();

        var kestrel = gateway.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        kestrel.Limits.MaxRequestBodySize.Should().Be(52_428_800);
    }

    [Fact]
    public async Task Slow_upstream_responses_time_out_with_a_504_problem()
    {
        var settings = DisableLimits();
        settings["Gateway:Timeouts:DefaultSeconds"] = "1";
        await using var gateway = _fixture.CreateGateway(settings);
        using var client = gateway.CreateClient();

        using var response = await client.GetAsync("/api/speed-reading/capabilities?delayMs=4000");

        await AssertProblemAsync(response, HttpStatusCode.GatewayTimeout, "https://eduplatform.dev/problems/gateway-timeout");
    }

    [Theory]
    [InlineData("GET", "/api/speed-reading/reading-texts/7d0a9f53-6f43-4bdb-8f6c-b4d4a7a2f0aa/export/pdf")]
    [InlineData("POST", "/api/speed-reading/reading-texts/export/docx")]
    [InlineData("POST", "/api/speed-reading/reading-texts/import/excel")]
    [InlineData("GET", "/api/speed-reading/reports/snapshots/7d0a9f53-6f43-4bdb-8f6c-b4d4a7a2f0aa/export/pdf")]
    [InlineData("POST", "/api/users/bulk/import")]
    [InlineData("GET", "/api/users/bulk/export")]
    [InlineData("POST", "/api/coaching-admin/catalog-imports")]
    [InlineData("GET", "/api/data-privacy/export")]
    [InlineData("GET", "/api/reports/comparative")]
    [InlineData("GET", "/api/coaching-admin/cms/newsletter/subscribers/export")]
    public async Task Export_and_import_endpoints_get_the_long_running_timeout(string method, string path)
    {
        var settings = DisableLimits();
        settings["Gateway:Timeouts:DefaultSeconds"] = "1";
        settings["Gateway:Timeouts:LongRunningSeconds"] = "10";
        await using var gateway = _fixture.CreateGateway(settings);
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"{path}?delayMs=1800");
        request.Headers.Authorization = Bearer();
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Notification_hub_is_exempt_from_request_timeouts()
    {
        var settings = DisableLimits();
        settings["Gateway:Timeouts:DefaultSeconds"] = "1";
        await using var gateway = _fixture.CreateGateway(settings);
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/hubs/notifications/negotiate?delayMs=1800");
        request.Headers.Authorization = Bearer();

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Every_cluster_gets_health_checks_and_a_long_activity_timeout_without_editing_the_route_table()
    {
        await using var gateway = _fixture.CreateGateway(DisableLimits());
        using var _ = gateway.CreateClient();
        var lookup = gateway.Services.GetRequiredService<IProxyStateLookup>();

        foreach (var clusterId in GatewayFactory.ClusterIds)
        {
            lookup.TryGetCluster(clusterId, out var cluster).Should().BeTrue(clusterId);
            var config = cluster!.Model.Config;
            config.HealthCheck!.AvailableDestinationsPolicy.Should().Be("HealthyOrPanic");
            config.HealthCheck.Passive!.Enabled.Should().BeTrue();
            config.HealthCheck.Active!.Enabled.Should().BeTrue();
            config.HealthCheck.Active.Path.Should().Be("/health/live");
            config.HttpRequest!.ActivityTimeout.Should().Be(TimeSpan.FromSeconds(330));
        }
    }

    [Fact]
    public async Task Proxy_pipeline_routes_to_healthy_destinations_with_load_balancing()
    {
        await using var gateway = _fixture.CreateGateway(DisableLimits());
        using var client = gateway.CreateClient();

        using var response = await client.GetAsync("/api/speed-reading/capabilities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lookup = gateway.Services.GetRequiredService<IProxyStateLookup>();
        lookup.TryGetCluster("speed-reading-cluster", out var cluster).Should().BeTrue();
        cluster!.DestinationsState.AvailableDestinations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Upstream_connections_are_recycled_so_new_replicas_receive_traffic()
    {
        await using var gateway = _fixture.CreateGateway(DisableLimits());
        using var _ = gateway.CreateClient();

        var factory = gateway.Services.GetRequiredService<IForwarderHttpClientFactory>();
        factory.Should().BeOfType<GatewayForwarderHttpClientFactory>();

        var handler = new SocketsHttpHandler();
        GatewayForwarderHttpClientFactory.ApplyConnectionPolicy(handler, TimeSpan.FromMinutes(2));
        handler.PooledConnectionLifetime.Should().Be(TimeSpan.FromMinutes(2));
    }

    private static Dictionary<string, string?> DisableLimits() => new()
    {
        ["Gateway:RateLimiting:Enabled"] = "false",
        ["REDIS_HOST"] = "127.0.0.1",
        ["REDIS_PORT"] = "1"
    };

    private static AuthenticationHeaderValue Bearer() =>
        new("Bearer", GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["SystemAdmin"]));

    private static HttpRequestMessage AuthorizedPost(string path, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.Authorization = Bearer();
        return request;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(type);
        body.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
