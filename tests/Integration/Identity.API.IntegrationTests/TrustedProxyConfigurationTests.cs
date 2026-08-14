using System.Net;
using EduPlatform.Gateway;
using FluentAssertions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

public sealed class TrustedProxyConfigurationTests
{
    [Fact]
    public void Create_WithNoTrustedProxyEntries_DoesNotTrustForwardedHeaders()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = TrustedProxyConfiguration.Create(configuration);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
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
                ["ForwardedHeaders:KnownProxies:0"] = "192.0.2.10",
                ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8",
                ["ForwardedHeaders:ForwardLimit"] = "2"
            })
            .Build();

        var options = TrustedProxyConfiguration.Create(configuration);

        options.ForwardLimit.Should().Be(2);
        options.KnownProxies.Should().ContainSingle().Which.Should().Be(IPAddress.Parse("192.0.2.10"));
        options.KnownNetworks.Should().ContainSingle().Which.Contains(IPAddress.Parse("10.42.0.7")).Should().BeTrue();
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
}
