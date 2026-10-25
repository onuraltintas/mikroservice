using System.Net;
using EduPlatform.Gateway.UnitTests.Infrastructure;

namespace EduPlatform.Gateway.UnitTests;

/// <summary>
/// The gateway is the only component allowed to state the caller's address to services.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class GatewayClientAddressTests
{
    private const string ClientIpHeader = "X-EduPlatform-Client-IP";
    private readonly BackendFixture _fixture;

    public GatewayClientAddressTests(BackendFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/api/speed-reading/capabilities")]
    [InlineData("/api/coaching/cms/home")]
    [InlineData("/api/platform/legal-pages")]
    public async Task Every_proxied_request_carries_the_gateway_resolved_address_instead_of_the_caller_copy(string path)
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        var clientIp = GatewayFactory.RandomClientIp();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(GatewayFactory.TestRemoteIpHeader, clientIp);
        request.Headers.Add(ClientIpHeader, "198.18.0.66");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echo = await EchoResponse.ReadAsync(response);
        echo.Headers[ClientIpHeader].Should().Be(clientIp);
    }

    [Fact]
    public async Task Caller_copy_is_dropped_when_the_gateway_cannot_resolve_an_address()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/speed-reading/capabilities");
        request.Headers.Add(ClientIpHeader, "198.18.0.66");

        using var response = await client.SendAsync(request);

        var echo = await EchoResponse.ReadAsync(response);
        echo.Headers.Should().NotContainKey(ClientIpHeader);
    }

    [Fact]
    public async Task Ipv4_mapped_addresses_are_forwarded_in_ipv4_form()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/speed-reading/capabilities");
        request.Headers.Add(GatewayFactory.TestRemoteIpHeader, "::ffff:10.20.30.40");

        using var response = await client.SendAsync(request);

        var echo = await EchoResponse.ReadAsync(response);
        echo.Headers[ClientIpHeader].Should().Be("10.20.30.40");
    }

    [Fact]
    public async Task Services_receive_a_single_x_forwarded_for_value_with_the_resolved_client()
    {
        await using var gateway = _fixture.CreateGateway();
        using var client = gateway.CreateClient();
        var clientIp = GatewayFactory.RandomClientIp();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new("Bearer", GatewayFactory.CreateToken(Guid.NewGuid().ToString()));
        request.Headers.Add(GatewayFactory.TestRemoteIpHeader, clientIp);
        request.Headers.Add("X-Forwarded-For", "198.18.0.66");

        using var response = await client.SendAsync(request);

        var echo = await EchoResponse.ReadAsync(response);
        echo.Headers["X-Forwarded-For"].Should().Be(clientIp);
    }
}
