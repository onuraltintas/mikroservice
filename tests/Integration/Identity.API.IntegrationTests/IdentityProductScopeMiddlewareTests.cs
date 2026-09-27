using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using EduPlatform.Shared.Security.Services;
using Identity.API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

public sealed class IdentityProductScopeMiddlewareTests
{
    private const string ServiceKey = "test-service-key-with-at-least-32-bytes";

    [Theory]
    [InlineData("coaching")]
    [InlineData("speed-reading")]
    public async Task RecognizedProductClaim_ShouldContinue(string product)
    {
        var (middleware, context, nextCalled) = CreateMiddleware(true, ("platform_product", product));

        await middleware.InvokeAsync(context, CreateConfiguration());

        Assert.True(nextCalled());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown-product")]
    public async Task MissingOrUnknownProductClaim_ShouldReturnForbidden(string? product)
    {
        var claims = product is null
            ? Array.Empty<(string Type, string Value)>()
            : [("platform_product", product)];
        var (middleware, context, nextCalled) = CreateMiddleware(true, claims);

        await middleware.InvokeAsync(context, CreateConfiguration());

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task SystemAdministratorWithoutProductClaim_ShouldContinue()
    {
        var (middleware, context, nextCalled) = CreateMiddleware(
            true,
            (ClaimTypes.Role, "SystemAdmin"));

        await middleware.InvokeAsync(context, CreateConfiguration());

        Assert.True(nextCalled());
    }

    [Fact]
    public async Task AnonymousRequest_ShouldContinueToAuthorization()
    {
        var (middleware, context, nextCalled) = CreateMiddleware(false);

        await middleware.InvokeAsync(context, CreateConfiguration());

        Assert.True(nextCalled());
    }

    [Fact]
    public async Task AuthenticatedAllowAnonymousEndpoint_ShouldAllowReauthenticationWithoutProductClaim()
    {
        var (middleware, context, nextCalled) = CreateMiddleware(true);
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "anonymous-auth-endpoint"));

        await middleware.InvokeAsync(context, CreateConfiguration());

        Assert.True(nextCalled());
    }

    [Fact]
    public async Task TrustedInternalServiceRequest_ShouldContinueWithoutUserProductScope()
    {
        var (middleware, context, nextCalled) = CreateMiddleware(true);
        context.Request.Headers[InternalServiceAuthentication.HeaderName] = ServiceKey;

        await middleware.InvokeAsync(context, CreateConfiguration());

        Assert.True(nextCalled());
    }

    private static (IdentityProductScopeMiddleware Middleware, DefaultHttpContext Context, Func<bool> NextCalled)
        CreateMiddleware(bool isAuthenticated, params (string Type, string Value)[] claims)
    {
        var called = false;
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            isAuthenticated ? "test" : null,
            ClaimTypes.Name,
            ClaimTypes.Role));

        return (new IdentityProductScopeMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }), context, () => called);
    }

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Internal:ServiceApiKey"] = ServiceKey
        })
        .Build();
}
