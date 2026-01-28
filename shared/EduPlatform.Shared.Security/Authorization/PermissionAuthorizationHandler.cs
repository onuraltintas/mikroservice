using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Shared.Security.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User == null)
        {
            return Task.CompletedTask;
        }

        // Check if user has "permission" claim with the required value
        var permissions = context.User.Claims
            .Where(x => x.Type == "permission" && x.Value == requirement.Permission);

        if (permissions.Any())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
