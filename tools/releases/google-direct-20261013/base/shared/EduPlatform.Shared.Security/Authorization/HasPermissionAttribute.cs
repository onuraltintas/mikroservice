using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Shared.Security.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute, IMfaCategoryMetadata
{
    public HasPermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = permission;
        Category = MfaOperationCategories.TryGetForPermission(permission, out var category)
            ? category
            : null;
    }

    public string Permission { get; }

    public string? Category { get; }
}
