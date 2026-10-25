using EduPlatform.Shared.Security.Services;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Identity.API.Security;

public sealed class IdentityProductScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || context.User.IsInRole("SystemAdmin")
            || InternalServiceAuthentication.IsValid(context.Request, configuration)
            || context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        var productClaim = context.User.FindFirst("platform_product")?.Value;
        if (PlatformProductExtensions.TryParseRouteValue(productClaim, out _))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            code = "ProductScopeRequired",
            message = "Bu işlem için geçerli bir ürün oturumu gereklidir. Lütfen ilgili platforma yeniden giriş yapın."
        }, context.RequestAborted);
    }
}
