using System.Security.Claims;
using Coaching.Application.Subscriptions;
using Microsoft.Extensions.Caching.Memory;

namespace Coaching.API.Security;

public sealed class CoachingSubscriptionAccessMiddleware(RequestDelegate next)
{
    /// <summary>Roadmap D1: how long an allowed student is not re-checked. Refusals are never remembered.</summary>
    public static readonly TimeSpan AllowedFor = TimeSpan.FromMinutes(1);

    public async Task InvokeAsync(HttpContext context, ICoachingSubscription subscriptions)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !context.User.IsInRole("Student")
            || HasStaffRole(context.User)
            || !CoachingSubscriptionAccessRules.RequiresSubscription(context.Request.Path.Value ?? string.Empty))
        {
            await next(context);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var cache = context.RequestServices?.GetService<IMemoryCache>();
        var cacheKey = "coaching-subscription-access:" + userId.ToString("N");
        if (cache?.TryGetValue(cacheKey, out _) == true)
        {
            await next(context);
            return;
        }

        var access = await subscriptions.GetMyAccessAsync(userId, context.RequestAborted);
        if (access.HasAccess) cache?.Set(cacheKey, true, AllowedFor);
        if (!access.HasAccess)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "CoachingSubscriptionRequired",
                message = "Koçluk aboneliğiniz etkin değil. Devam etmek için planınızı yenileyin veya kurum yöneticinizle iletişime geçin."
            }, context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool HasStaffRole(ClaimsPrincipal user) =>
        new[] { "SystemAdmin", "Admin", "Teacher", "InstitutionAdmin", "InstitutionOwner", "Coach" }
            .Any(user.IsInRole);
}
