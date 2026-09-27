using System.Security.Claims;
using EduPlatform.Shared.Security.Middleware;
using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

public sealed class ProductScopeMiddlewareTests
{
    private const string ServiceKey = "test-service-key-with-at-least-32-bytes";

    [Fact]
    public async Task MatchingProductClaim_ShouldContinue()
    {
        var nextCalled = false;
        var context = CreateContext(isAuthenticated: true, ("platform_product", "coaching"));
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration());

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task DifferentProductClaim_ShouldReturnForbidden()
    {
        var nextCalled = false;
        var context = CreateContext(isAuthenticated: true, ("platform_product", "speed-reading"));
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration());

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MissingProductClaim_ShouldReturnForbidden()
    {
        var nextCalled = false;
        var context = CreateContext(isAuthenticated: true);
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration());

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task SystemAdminWithoutProductClaim_ShouldContinue()
    {
        var nextCalled = false;
        var context = CreateContext(isAuthenticated: true, (ClaimTypes.Role, "SystemAdmin"));
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration());

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AnonymousRequest_ShouldContinueToAuthorization()
    {
        var nextCalled = false;
        var context = CreateContext(isAuthenticated: false);
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration());

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task TrustedInternalServiceRequest_ShouldBypassUserProductScope()
    {
        var nextCalled = false;
        var context = CreateContext(isAuthenticated: true, ("platform_product", "coaching"));
        context.Request.Headers[InternalServiceAuthentication.HeaderName] = ServiceKey;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration());

        nextCalled.Should().BeTrue();
    }

    private static ProductScopeMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, "coaching");

    private static DefaultHttpContext CreateContext(
        bool isAuthenticated,
        params (string Type, string Value)[] claims)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            isAuthenticated ? "test" : null,
            ClaimTypes.Name,
            ClaimTypes.Role);
        context.User = new ClaimsPrincipal(identity);
        return context;
    }

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Internal:ServiceApiKey"] = ServiceKey
        })
        .Build();
}
