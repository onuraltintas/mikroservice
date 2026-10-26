using System.Security.Claims;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Identity.API.IntegrationTests;

public sealed class ProductScopeMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldRejectAnAuthenticatedTokenFromAnotherProduct()
    {
        var nextCalled = false;
        var context = CreateContext("coaching");
        var middleware = new ProductScopeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, "speed-reading");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldAllowMatchingProductAndGlobalSystemAdministrator()
    {
        var matchingProductNextCalled = false;
        var matchingProduct = CreateContext("speed-reading");
        await new ProductScopeMiddleware(_ =>
        {
            matchingProductNextCalled = true;
            return Task.CompletedTask;
        }, "speed-reading").InvokeAsync(matchingProduct);

        var systemAdminNextCalled = false;
        var systemAdmin = CreateContext(product: null, "SystemAdmin");
        await new ProductScopeMiddleware(_ =>
        {
            systemAdminNextCalled = true;
            return Task.CompletedTask;
        }, "coaching").InvokeAsync(systemAdmin);

        Assert.True(matchingProductNextCalled);
        Assert.True(systemAdminNextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ShouldLeaveAnonymousEndpointsForTheirOwnAuthorizationPolicy()
    {
        var nextCalled = false;
        var context = CreateContext(product: null);
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "anonymous"));

        await new ProductScopeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, "coaching").InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext CreateContext(string? product, string? role = null)
    {
        var claims = new List<Claim>();
        if (product is not null) claims.Add(new Claim("platform_product", product));
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        var identity = new ClaimsIdentity(claims, "Bearer", ClaimTypes.Name, ClaimTypes.Role);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(identity);
        return context;
    }
}
