using System.Security.Claims;
using EduPlatform.Shared.Security.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// V2 (parent panel, decision 01.11.2026): a parent signs in to the Coaching portal, so the speed-reading service accepts
/// a Coaching-product token only for the Parent role and only on the parent read routes.
/// </summary>
public sealed class ProductScopeMiddlewareTests
{
    private static readonly ProductScopeException ParentReads = new("coaching", "Parent", "/api/speed-reading/parents/");

    [Theory]
    [InlineData("coaching")]
    [InlineData("speed-reading")]
    public async Task Parent_token_reads_the_parent_routes(string product)
    {
        var result = await Run("speed-reading", [ParentReads], product, "Parent", "/api/speed-reading/parents/me/children/1/overview");
        result.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Coaching_token_without_the_parent_role_is_rejected()
    {
        var result = await Run("speed-reading", [ParentReads], "coaching", "Student", "/api/speed-reading/parents/me/children/1/overview");
        result.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Theory]
    [InlineData("/api/speed-reading/progress/reading-history")]
    [InlineData("/api/speed-reading/parentsx/me")]
    [InlineData("/api/speed-reading/parents")]
    [InlineData("/api/speed-reading/teachers/me/students")]
    public async Task Coaching_parent_token_is_rejected_outside_the_parent_routes(string path)
    {
        var result = await Run("speed-reading", [ParentReads], "coaching", "Parent", path);
        result.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Other_products_stay_rejected_on_the_parent_routes()
    {
        var result = await Run("speed-reading", [ParentReads], "course-management", "Parent", "/api/speed-reading/parents/me/children/1/overview");
        result.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Services_without_the_exception_keep_the_strict_product_check()
    {
        (await Run("speed-reading", [], "coaching", "Parent", "/api/speed-reading/parents/me/children/1/overview"))
            .Should().Be(StatusCodes.Status403Forbidden);
        (await Run("coaching", [], "speed-reading", "Parent", "/api/coaching/parents/me"))
            .Should().Be(StatusCodes.Status403Forbidden);
        (await Run("coaching", [], "coaching", "Student", "/api/coaching/anything"))
            .Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public void Exceptions_must_name_a_product_a_role_and_a_rooted_prefix()
    {
        FluentActions.Invoking(() => new ProductScopeException("", "Parent", "/api/x/")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ProductScopeException("coaching", " ", "/api/x/")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ProductScopeException("coaching", "Parent", "api/x/")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ProductScopeException("coaching", "Parent", "/api/x")).Should().Throw<ArgumentException>();
    }

    private static async Task<int> Run(string serviceProduct, ProductScopeException[] exceptions, string tokenProduct, string role, string path)
    {
        var reached = false;
        var middleware = new ProductScopeMiddleware(_ => { reached = true; return Task.CompletedTask; }, serviceProduct, exceptions);
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role),
             new Claim("platform_product", tokenProduct)], "test"));
        await middleware.InvokeAsync(context, new ConfigurationBuilder().Build());
        return reached ? StatusCodes.Status200OK : context.Response.StatusCode;
    }
}
