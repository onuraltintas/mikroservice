using System.Net;
using EduPlatform.Gateway.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EduPlatform.Gateway.UnitTests.RateLimiting;

/// <summary>
/// The shipped rule set (services/api-gateway/appsettings.json) resolved per request.
/// </summary>
public sealed class RateLimitRuleSetTests
{
    private static readonly GatewayRateLimitOptions Shipped = LoadShippedOptions();

    [Theory]
    [InlineData("/api/auth/forgot-password", "POST", "auth-email", 5, 900)]
    [InlineData("/api/auth/resend-verification-email", "POST", "auth-email", 5, 900)]
    [InlineData("/api/auth/reset-password", "POST", "auth-password-reset", 10, 900)]
    [InlineData("/api/auth/confirm-email", "POST", "auth-email-confirmation", 10, 900)]
    [InlineData("/api/auth/coaching/register/student", "POST", "auth-registration", 15, 60)]
    [InlineData("/api/auth/coaching/register/join", "POST", "auth-registration", 15, 60)]
    [InlineData("/api/auth/speed-reading/register/join", "POST", "auth-registration", 15, 60)]
    [InlineData("/api/auth/speed-reading/register/institution", "POST", "auth-registration", 15, 60)]
    [InlineData("/api/auth/refresh-token", "POST", "auth-refresh", 300, 60)]
    [InlineData("/api/auth/course-management/refresh-token", "POST", "auth-refresh", 300, 60)]
    [InlineData("/api/auth/login", "POST", "auth-login", 60, 60)]
    [InlineData("/api/auth/coaching/login", "POST", "auth-login", 60, 60)]
    [InlineData("/api/auth/speed-reading/login", "POST", "auth-login", 60, 60)]
    [InlineData("/api/auth/course-management/login", "POST", "auth-login", 60, 60)]
    [InlineData("/api/auth/course-management/login/coaching", "POST", "auth-login", 60, 60)]
    [InlineData("/api/auth/google-login", "POST", "auth-login", 60, 60)]
    [InlineData("/api/auth/coaching/google", "POST", "auth-login", 60, 60)]
    [InlineData("/api/auth/mfa/verify", "POST", "auth-login", 60, 60)]
    [InlineData("/api/auth/course-management/mfa/verify", "POST", "auth-login", 60, 60)]
    [InlineData("/api/auth/captcha-config", "GET", "auth", 120, 60)]
    [InlineData("/api/auth/staff-session/products", "GET", "auth", 120, 60)]
    [InlineData("/api/auth/revoke-token", "POST", "auth", 120, 60)]
    [InlineData("/api/support/submit", "POST", "support-submit", 10, 60)]
    [InlineData("/api/speed-reading/cms/contact", "POST", "speed-reading-public-write", 8, 600)]
    [InlineData("/api/speed-reading/cms/newsletter/subscribe", "POST", "speed-reading-public-write", 8, 600)]
    [InlineData("/api/coaching/cms/newsletter/subscriptions", "POST", "coaching-newsletter-public-write", 8, 600)]
    [InlineData("/api/calendar/feed/0123456789abcdef.ics", "GET", "coaching-calendar-feed", 60, 60)]
    [InlineData("/api/calendar/feed/0123456789abcdef.ics", "HEAD", "coaching-calendar-feed", 60, 60)]
    [InlineData("/api/speed-reading/bank-transfer/requests", "POST", "bank-transfer-request", 5, 600)]
    public void Shipped_rules_resolve_the_expected_limit(string path, string method, string name, int limit, int windowSeconds)
    {
        var ruleSet = new RateLimitRuleSet(Shipped.Rules);

        var rule = ruleSet.Match(method, path);

        rule.Should().NotBeNull();
        rule!.Name.Should().Be(name);
        rule.PermitLimit.Should().Be(limit);
        rule.Window.Should().Be(TimeSpan.FromSeconds(windowSeconds));
    }

    [Theory]
    [InlineData("/api/speed-reading/bank-transfer/requests", "GET")]
    [InlineData("/api/speed-reading/cms/newsletter/unsubscribe-info", "GET")]
    [InlineData("/api/coaching/cms/newsletter/subscriptions", "GET")]
    [InlineData("/api/users/me", "GET")]
    [InlineData("/health", "GET")]
    [InlineData("/api/authorities", "GET")]
    public void Unlisted_requests_have_no_route_rule(string path, string method)
    {
        new RateLimitRuleSet(Shipped.Rules).Match(method, path).Should().BeNull();
    }

    [Fact]
    public void Rules_with_the_same_name_share_one_budget_key()
    {
        var ruleSet = new RateLimitRuleSet(Shipped.Rules);

        var contact = ruleSet.Match("POST", "/api/speed-reading/cms/contact");
        var newsletter = ruleSet.Match("POST", "/api/speed-reading/cms/newsletter/subscribe");

        contact!.Name.Should().Be(newsletter!.Name);
    }

    [Fact]
    public void Shipped_authenticated_user_limit_is_600_per_minute_and_skips_the_hub()
    {
        Shipped.AuthenticatedUser.Enabled.Should().BeTrue();
        Shipped.AuthenticatedUser.PermitLimit.Should().Be(600);
        Shipped.AuthenticatedUser.WindowSeconds.Should().Be(60);
        Shipped.AuthenticatedUser.ExcludedPathPrefixes.Should().Contain("/hubs");
    }

    [Theory]
    [InlineData("", 5, 60, "/api/x", "name")]
    [InlineData("rule", 0, 60, "/api/x", "PermitLimit")]
    [InlineData("rule", 5, 0, "/api/x", "WindowSeconds")]
    [InlineData("rule", 5, 60, "api/x", "start with '/'")]
    [InlineData("rule", 5, 60, null, "Paths or PathPrefixes")]
    public void Invalid_rules_fail_fast(string name, int limit, int window, string? path, string expectedMessage)
    {
        var options = new GatewayRateLimitOptions
        {
            Rules =
            [
                new GatewayRateLimitRule
                {
                    Name = name,
                    PermitLimit = limit,
                    WindowSeconds = window,
                    Paths = path is null ? [] : [path]
                }
            ]
        };

        var validate = () => options.Validate();

        validate.Should().Throw<InvalidOperationException>().WithMessage($"*{expectedMessage}*");
    }

    [Fact]
    public void Ipv4_and_ipv4_mapped_addresses_share_a_partition()
    {
        ClientPartition.ForAddress(IPAddress.Parse("10.1.2.3"))
            .Should().Be(ClientPartition.ForAddress(IPAddress.Parse("::ffff:10.1.2.3")));
    }

    [Fact]
    public void Ipv6_clients_are_grouped_by_their_64_bit_prefix()
    {
        var first = ClientPartition.ForAddress(IPAddress.Parse("2001:db8:1:2:aaaa::1"));
        var sameNetwork = ClientPartition.ForAddress(IPAddress.Parse("2001:db8:1:2:bbbb::9"));
        var otherNetwork = ClientPartition.ForAddress(IPAddress.Parse("2001:db8:1:3::1"));

        first.Should().Be(sameNetwork);
        first.Should().NotBe(otherNetwork);
    }

    [Fact]
    public void Missing_address_uses_a_single_unknown_partition()
    {
        ClientPartition.ForAddress(null).Should().Be("ip:unknown");
    }

    [Fact]
    public void User_partition_uses_the_subject_claim()
    {
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim("sub", "user-42")],
            "Bearer"));

        ClientPartition.ForUser(context.User).Should().Be("user:user-42");
    }

    private static GatewayRateLimitOptions LoadShippedOptions()
    {
        var root = GatewayDeploymentConfigurationTests.FindRepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(root, "services", "api-gateway", "appsettings.json"))
            .Build();
        var options = configuration.GetSection(GatewayRateLimitOptions.SectionName).Get<GatewayRateLimitOptions>()
            ?? throw new InvalidOperationException("Gateway:RateLimiting is missing from appsettings.json.");
        options.Validate();
        return options;
    }
}
