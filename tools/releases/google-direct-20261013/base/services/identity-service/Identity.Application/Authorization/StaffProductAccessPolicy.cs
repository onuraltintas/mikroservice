using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Authorization;

public sealed record StaffPortalProductAccess(
    PlatformProduct Product,
    IReadOnlyList<string> Roles);

public static class StaffProductAccessPolicy
{
    public static IReadOnlyList<string> GetStaffRoles(User user, PlatformProduct product)
    {
        if (!user.IsActive
            || !user.HasProductAccess(product)
            || user.Roles.Any(userRole => userRole.Role is not null
                && !userRole.Role.IsDeleted
                && IsStaffPortalExcludedRole(userRole.Role.Name)))
        {
            return [];
        }

        return user.Roles
            .Where(userRole => userRole.Product == product
                && userRole.Role is not null
                && !userRole.Role.IsDeleted
                && IsStaffRole(userRole.Role.Name))
            .Select(userRole => userRole.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<StaffPortalProductAccess> GetProductAccesses(User user) =>
        Enum.GetValues<PlatformProduct>()
            .Select(product => new StaffPortalProductAccess(product, GetStaffRoles(user, product)))
            .Where(access => access.Roles.Count > 0)
            .ToArray();

    private static bool IsStaffRole(string roleName) =>
        string.Equals(roleName, "Teacher", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "InstitutionAdmin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "InstitutionOwner", StringComparison.OrdinalIgnoreCase);

    private static bool IsStaffPortalExcludedRole(string roleName) =>
        string.Equals(roleName, "SystemAdmin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "Editor", StringComparison.OrdinalIgnoreCase);
}
