using System.Security.Claims;
using EduPlatform.Shared.Security.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EduPlatform.Shared.Security.Middleware;

/// <summary>
/// An opt-in, narrow exception to the product check: a token of <paramref name="Product"/> is accepted only for an
/// authenticated user in <paramref name="Role"/> and only on paths under <paramref name="PathPrefix"/> (which must start and
/// end with "/"). V2 (parent panel, 01.11.2026): parents sign in to the Coaching portal and read a child's speed reading.
/// </summary>
public sealed record ProductScopeException
{
    public ProductScopeException(string Product, string Role, string PathPrefix)
    {
        if (string.IsNullOrWhiteSpace(Product))
            throw new ArgumentException("A product is required.", nameof(Product));
        if (string.IsNullOrWhiteSpace(Role))
            throw new ArgumentException("A role is required.", nameof(Role));
        if (string.IsNullOrWhiteSpace(PathPrefix) || !PathPrefix.StartsWith('/') || !PathPrefix.EndsWith('/') || PathPrefix.Length < 3)
            throw new ArgumentException("The path prefix must start and end with '/'.", nameof(PathPrefix));
        this.Product = Product;
        this.Role = Role;
        this.PathPrefix = PathPrefix;
    }

    public string Product { get; }
    public string Role { get; }
    public string PathPrefix { get; }

    internal bool Allows(HttpContext context, string? tokenProduct) =>
        string.Equals(tokenProduct, Product, StringComparison.OrdinalIgnoreCase)
        && context.User.IsInRole(Role)
        && context.Request.Path.StartsWithSegments(PathPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase, out var rest)
        && rest.HasValue && rest.Value!.Length > 1;
}

public sealed class ProductScopeMiddleware(RequestDelegate next, string product, ProductScopeException[]? exceptions = null)
{
    private readonly ProductScopeException[] exceptions = exceptions ?? [];

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || context.User.IsInRole("SystemAdmin")
            || InternalServiceAuthentication.IsValid(context.Request, configuration))
        {
            await next(context);
            return;
        }

        var tokenProduct = context.User.FindFirstValue("platform_product");
        if (string.Equals(tokenProduct, product, StringComparison.OrdinalIgnoreCase)
            || exceptions.Any(exception => exception.Allows(context, tokenProduct)))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            code = "ProductAccessDenied",
            message = "Bu hesaba bu platform için erişim izni verilmemiş."
        }, context.RequestAborted);
    }
}
