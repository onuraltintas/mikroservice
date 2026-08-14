using System.Net;
using EduPlatform.Gateway;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

public sealed class TrustedProxyConfigurationTests
{
    [Fact]
    public void Create_WithNoTrustedProxyEntries_DoesNotTrustForwardedHeaders()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = TrustedProxyConfiguration.Create(configuration);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.None);
        options.ForwardLimit.Should().Be(1);
        options.KnownProxies.Should().BeEmpty();
        options.KnownNetworks.Should().BeEmpty();
    }

    [Fact]
    public void Create_ParsesKnownProxiesNetworksAndForwardLimit()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies"] = "192.0.2.10, 192.0.2.11",
                ["ForwardedHeaders:KnownNetworks"] = "10.0.0.0/8, 172.16.0.0/12",
                ["ForwardedHeaders:ForwardLimit"] = "2"
            })
            .Build();

        var options = TrustedProxyConfiguration.Create(configuration);

        options.ForwardLimit.Should().Be(2);
        options.KnownProxies.Should().HaveCount(2);
        options.KnownProxies.Should().Contain(IPAddress.Parse("192.0.2.10"));
        options.KnownNetworks.Should().HaveCount(2);
        options.KnownNetworks.Should().Contain(network => network.Contains(IPAddress.Parse("10.42.0.7")));
        options.KnownNetworks.Should().Contain(network => network.Contains(IPAddress.Parse("172.20.0.7")));
    }

    [Fact]
    public void Create_WithInvalidTrustedProxyAddress_FailsFast()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "not-an-ip"
            })
            .Build();

        var action = () => TrustedProxyConfiguration.Create(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*KnownProxies*");
    }

    [Fact]
    public void Create_WithInvalidTrustedProxyNetwork_FailsFast()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "not-a-network"
            })
            .Build();

        var action = () => TrustedProxyConfiguration.Create(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*KnownNetworks*");
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    public void Create_WithCatchAllTrustedNetwork_FailsFast(string network)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = network
            })
            .Build();

        var action = () => TrustedProxyConfiguration.Create(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*KnownNetworks*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("11")]
    public void Create_WithForwardLimitOutsideSafeRange_FailsFast(string forwardLimit)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:ForwardLimit"] = forwardLimit
            })
            .Build();

        var action = () => TrustedProxyConfiguration.Create(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ForwardLimit*");
    }

    [Fact]
    public async Task ForwardedHeadersMiddleware_UsesClientIpOnlyForTrustedProxy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "192.0.2.10"
            })
            .Build();
        var options = TrustedProxyConfiguration.Create(configuration);
        var context = CreateForwardedRequest("192.0.2.10", "198.51.100.44");
        IPAddress? resolvedAddress = null;

        var middleware = new ForwardedHeadersMiddleware(
            next: httpContext =>
            {
                resolvedAddress = httpContext.Connection.RemoteIpAddress;
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            Options.Create(options));

        await middleware.Invoke(context);

        resolvedAddress.Should().Be(IPAddress.Parse("198.51.100.44"));
    }

    [Fact]
    public async Task ForwardedHeadersMiddleware_IgnoresClientSuppliedIpFromUnknownProxy()
    {
        var options = TrustedProxyConfiguration.Create(new ConfigurationBuilder().Build());
        var context = CreateForwardedRequest("192.0.2.10", "198.51.100.44");
        IPAddress? resolvedAddress = null;

        var middleware = new ForwardedHeadersMiddleware(
            next: httpContext =>
            {
                resolvedAddress = httpContext.Connection.RemoteIpAddress;
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            Options.Create(options));

        await middleware.Invoke(context);

        resolvedAddress.Should().Be(IPAddress.Parse("192.0.2.10"));
    }

    private static DefaultHttpContext CreateForwardedRequest(string proxyAddress, string clientAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(proxyAddress);
        context.Request.Headers["X-Forwarded-For"] = clientAddress;
        return context;
    }
}
