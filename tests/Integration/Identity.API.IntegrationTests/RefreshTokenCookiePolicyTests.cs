using FluentAssertions;
using Identity.API.Security;
using Identity.Application.Commands.Login;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

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

    [Fact]
    public void RememberMeDisabled_ShouldCreateBrowserSessionCookie()
    {
        var options = RefreshTokenCookiePolicy.CreateOptions(
            isProduction: true,
            DateTimeOffset.UtcNow.AddDays(7),
            isPersistent: false);

        options.Expires.Should().BeNull();
        options.HttpOnly.Should().BeTrue();
        options.Secure.Should().BeTrue();
    }

    [Fact]
    public void IssueSession_ShouldKeepRefreshTokenOutOfResponseBody()
    {
        var context = new DefaultHttpContext();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(7);
        var login = new LoginResponse(
            "access-token",
            "browser-secret-refresh-token",
            refreshExpiresAt,
            IsPersistent: true);

        var response = RefreshTokenCookiePolicy.Issue(
            context.Response,
            login,
            isProduction: true);

        var json = JsonSerializer.Serialize(response);
        json.Should().Contain("access-token");
        json.Should().NotContain("browser-secret-refresh-token");
        json.Should().NotContain("RefreshToken");
        context.Response.Headers.SetCookie.ToString().ToLowerInvariant().Should()
            .ContainAll("httponly", "secure", "samesite=strict", "path=/api/auth");
    }

    [Fact]
    public void CourseManagementCookie_ShouldBeScopedToItsOwnAuthRoutes()
    {
        var options = RefreshTokenCookiePolicy.CreateCourseManagementOptions(
            isProduction: true,
            DateTimeOffset.UtcNow.AddDays(7));

        RefreshTokenCookiePolicy.CourseManagementCookieName.Should().NotBe(RefreshTokenCookiePolicy.CookieName);
        options.HttpOnly.Should().BeTrue();
        options.Secure.Should().BeTrue();
        options.SameSite.Should().Be(SameSiteMode.Strict);
        options.Path.Should().Be("/api/auth/course-management");
    }

    [Fact]
    public void IssueCourseManagementSession_ShouldUseOnlyCourseManagementCookie()
    {
        var context = new DefaultHttpContext();
        var login = new LoginResponse("course-access-token", "course-refresh-token", DateTime.UtcNow.AddDays(7), true);

        var response = RefreshTokenCookiePolicy.IssueCourseManagement(context.Response, login, isProduction: true);

        response.AccessToken.Should().Be("course-access-token");
        context.Response.Headers.SetCookie.ToString().Should().Contain(RefreshTokenCookiePolicy.CourseManagementCookieName);
        context.Response.Headers.SetCookie.ToString().Should().Contain("path=/api/auth/course-management");
        context.Response.Headers.SetCookie.ToString().Should().NotContain(RefreshTokenCookiePolicy.CookieName + "=");
    }
}
