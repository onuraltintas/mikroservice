using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace EduPlatform.Shared.Security.Authorization;

public sealed class ProductScopeMiddleware(RequestDelegate next, string requiredProduct)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || context.User.Identity?.IsAuthenticated != true
            || context.User.IsInRole("SystemAdmin")
            || string.Equals(
                context.User.FindFirst("platform_product")?.Value,
                requiredProduct,
                StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}
