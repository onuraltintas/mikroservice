using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Shared.Security.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = permission;
    }
}
