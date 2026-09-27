using System.Security.Claims;
using EduPlatform.Shared.Security.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EduPlatform.Shared.Security.Middleware;

public sealed class ProductScopeMiddleware(RequestDelegate next, string product)
{
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
        if (string.Equals(tokenProduct, product, StringComparison.OrdinalIgnoreCase))
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
