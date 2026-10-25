using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Shared.Security.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
