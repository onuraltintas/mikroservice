using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EduPlatform.Gateway.UnitTests.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;

namespace EduPlatform.Gateway.UnitTests;

/// <summary>
/// Freezes the gateway behaviour that other services and the Angular clients depend on.
/// These tests must stay green through every hardening phase.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class GatewayContractTests
{
    private readonly BackendFixture _fixture;

    public GatewayContractTests(BackendFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/api/auth/login", "identity")]
    [InlineData("/api/users", "identity")]
    [InlineData("/api/users/me", "identity")]
    [InlineData("/api/institutions/abc", "identity")]
    [InlineData("/api/configurations/public/maintenance", "identity")]
    [InlineData("/api/assignments/42", "coaching")]
    [InlineData("/api/coaching/cms/home", "coaching")]
    [InlineData("/api/coaching-admin/catalog-imports", "coaching")]
    [InlineData("/api/data-privacy/export", "coaching")]
    [InlineData("/api/speed-reading", "speed-reading")]
    [InlineData("/api/speed-reading/capabilities", "speed-reading")]
    [InlineData("/api/speed-reading/reading-texts/export/pdf", "speed-reading")]
    [InlineData("/api/support/submit", "notification")]
    [InlineData("/api/notifications", "notification")]
    [InlineData("/api/course-management/notifications", "notification")]
    [InlineData("/api/course-management/v1/me/profile", "course-management")]
    public async Task Routes_forward_to_the_owning_cluster_with_the_original_path(string path, string expectedCluster)
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GatewayFactory.CreateToken(Guid.NewGuid().ToString()));
        request.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echo = await EchoResponse.ReadAsync(response);
        echo.Cluster.Should().Be(expectedCluster);
        echo.Path.Should().Be($"/{expectedCluster}{path}");
    }

    [Fact]
    public async Task Protected_route_without_token_is_rejected_at_the_gateway()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();

        using var response = await client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Anonymous_route_is_forwarded_without_token()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();

        using var response = await client.GetAsync("/api/speed-reading/capabilities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/coaching/cms/newsletter/subscriptions")]
    [InlineData("/api/speed-reading/cms/newsletter/subscribe")]
    [InlineData("/api/speed-reading/cms/contact")]
    [InlineData("/api/speed-reading/bank-transfer/requests")]
    public async Task Public_write_routes_receive_the_gateway_resolved_client_ip_not_the_caller_copy(string path)
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { email = "student@example.com" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GatewayFactory.CreateToken(Guid.NewGuid().ToString()));
        var clientIp = GatewayFactory.RandomClientIp();
        request.Headers.Add(GatewayFactory.TestRemoteIpHeader, clientIp);
        request.Headers.Add("X-EduPlatform-Client-IP", "198.18.0.66");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echo = await EchoResponse.ReadAsync(response);
        echo.Headers["X-EduPlatform-Client-IP"].Should().Be(clientIp);
    }

    [Fact]
    public async Task Gateway_service_list_is_available_to_system_admins_for_the_maintenance_screen()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gateway/services");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["SystemAdmin"]));

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var services = await response.Content.ReadFromJsonAsync<string[]>();
        services.Should().Contain(["identity", "coaching", "notification", "speed-reading"]);
    }

    [Theory]
    [InlineData("/api/course-management/v1/me/profile")]
    [InlineData("/api/course-management/notifications")]
    [InlineData("/api/course-management/notifications/unread-count")]
    public async Task Course_management_paths_are_not_routed_when_the_product_is_disabled(string path)
    {
        await using var gateway = _fixture.CreateGateway(new Dictionary<string, string?> { ["CourseManagement:Enabled"] = "false" });
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GatewayFactory.CreateToken(Guid.NewGuid().ToString()));
        request.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/billing/v1/catalog/coaching")]
    [InlineData("/api/billing/v1/admin/products")]
    public async Task Billing_paths_are_not_routed_when_billing_is_disabled(string path)
    {
        await using var gateway = _fixture.CreateGateway(new Dictionary<string, string?> { ["Billing:Enabled"] = "false" });
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["SystemAdmin"]));
        request.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Billing_catalog_is_public_and_the_rest_of_billing_needs_a_signed_in_user()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();

        using var catalog = new HttpRequestMessage(HttpMethod.Get, "/api/billing/v1/catalog/coaching");
        catalog.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());
        using var catalogResponse = await client.SendAsync(catalog);
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EchoResponse.ReadAsync(catalogResponse)).Cluster.Should().Be("billing");

        using var anonymous = new HttpRequestMessage(HttpMethod.Get, "/api/billing/v1/admin/products");
        anonymous.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());
        using var anonymousResponse = await client.SendAsync(anonymous);
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var signedIn = new HttpRequestMessage(HttpMethod.Get, "/api/billing/v1/admin/products");
        signedIn.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["SystemAdmin"]));
        signedIn.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());
        using var signedInResponse = await client.SendAsync(signedIn);
        signedInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EchoResponse.ReadAsync(signedInResponse)).Cluster.Should().Be("billing");
    }

    // eduivme.com (19.11.2026): visitors read the site without signing in; editing it needs a signed-in user (the
    // service itself admits only the system admin).
    [Fact]
    public async Task The_eduivme_site_is_public_and_its_admin_needs_a_signed_in_user()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();

        using var home = new HttpRequestMessage(HttpMethod.Get, "/api/site/home");
        home.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());
        using var homeResponse = await client.SendAsync(home);
        homeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EchoResponse.ReadAsync(homeResponse)).Cluster.Should().Be("site");

        using var anonymous = new HttpRequestMessage(HttpMethod.Get, "/api/site-admin/entries");
        anonymous.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());
        using var anonymousResponse = await client.SendAsync(anonymous);
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var signedIn = new HttpRequestMessage(HttpMethod.Get, "/api/site-admin/entries");
        signedIn.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["SystemAdmin"]));
        signedIn.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());
        using var signedInResponse = await client.SendAsync(signedIn);
        signedInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EchoResponse.ReadAsync(signedInResponse)).Cluster.Should().Be("site");
    }

    [Fact]
    public async Task Other_routes_and_the_service_list_are_unchanged_when_course_management_is_disabled()
    {
        await using var gateway = _fixture.CreateGateway(new Dictionary<string, string?> { ["CourseManagement:Enabled"] = "false" });
        using var client = gateway.CreateClient();
        var token = GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["SystemAdmin"]);

        using var routed = new HttpRequestMessage(HttpMethod.Get, "/api/notifications");
        routed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        routed.Headers.Add(GatewayFactory.TestRemoteIpHeader, GatewayFactory.RandomClientIp());
        using var routedResponse = await client.SendAsync(routed);
        routedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EchoResponse.ReadAsync(routedResponse)).Cluster.Should().Be("notification");

        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/gateway/services");
        list.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var listResponse = await client.SendAsync(list);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var services = await listResponse.Content.ReadFromJsonAsync<string[]>();
        services.Should().BeEquivalentTo(["identity", "coaching", "notification", "speed-reading", "billing", "site"]);
    }

    [Fact]
    public async Task Gateway_service_list_requires_authentication()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();

        using var response = await client.GetAsync("/api/gateway/services");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gateway_health_endpoints_stay_anonymous()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();

        using var health = await client.GetAsync("/health");
        using var live = await client.GetAsync("/health/live");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync()).Should().Contain("Healthy");
        live.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RedisFact]
    public async Task Service_maintenance_returns_503_with_the_message_field_the_admin_login_reads()
    {
        var settings = RedisTestServer.GatewaySettings();
        await using var gateway = _fixture.CreateGateway(settings);
        using var client = gateway.CreateClient();
        using var cache = RedisTestServer.CreateSharedCache();
        await cache.SetStringAsync("config:maintenance.notification", "true");
        try
        {
            using var userRequest = new HttpRequestMessage(HttpMethod.Get, "/api/notifications");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["Student"]));
            using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/api/notifications");
            adminRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                GatewayFactory.CreateToken(Guid.NewGuid().ToString(), roles: ["SystemAdmin"]));

            using var blocked = await client.SendAsync(userRequest);
            using var bypassed = await client.SendAsync(adminRequest);

            blocked.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            using var body = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
            body.RootElement.GetProperty("service").GetString().Should().Be("notification");
            bypassed.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await cache.RemoveAsync("config:maintenance.notification");
        }
    }
}
