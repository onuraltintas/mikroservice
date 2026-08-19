using FluentAssertions;
using Identity.API.Security;
using Microsoft.AspNetCore.Http;

namespace Identity.API.IntegrationTests;

public sealed class RefreshTokenCookiePolicyTests
{
    [Fact]
    public void ProductionCookie_ShouldBeHttpOnlySecureAndStrict()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        var options = RefreshTokenCookiePolicy.CreateOptions(
            isProduction: true,
            expiresAt);

        options.HttpOnly.Should().BeTrue();
        options.Secure.Should().BeTrue();
        options.SameSite.Should().Be(SameSiteMode.Strict);
        options.Path.Should().Be("/api/auth");
        options.Expires.Should().Be(expiresAt);
        options.IsEssential.Should().BeTrue();
    }

    [Fact]
    public void DevelopmentCookie_ShouldAllowLocalHttpWithoutWeakeningSameSiteOrHttpOnly()
    {
        var options = RefreshTokenCookiePolicy.CreateOptions(
            isProduction: false,
            DateTimeOffset.UtcNow.AddDays(1));

        options.HttpOnly.Should().BeTrue();
        options.Secure.Should().BeFalse();
        options.SameSite.Should().Be(SameSiteMode.Strict);
    }
}
