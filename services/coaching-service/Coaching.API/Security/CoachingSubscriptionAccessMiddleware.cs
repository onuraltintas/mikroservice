using System.Security.Claims;
using Coaching.Application.Subscriptions;

namespace Coaching.API.Security;

public sealed class CoachingSubscriptionAccessMiddleware(RequestDelegate next)
{
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

        var access = await subscriptions.GetMyAccessAsync(userId, context.RequestAborted);
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
