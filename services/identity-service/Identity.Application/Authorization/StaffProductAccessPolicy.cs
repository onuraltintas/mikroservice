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
                && (IsStaffRole(userRole.Role.Name)
                    || product == PlatformProduct.CourseManagement && userRole.Role.Name is "CourseTeacher" or "CourseInstitutionOwner"))
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

    /// <summary>
    /// User decision 23.11.2026: the product the staff portal opens after sign-in. Only maxkoç and Master Hızlı Okuma
    /// (maxkampüs keeps its own sign-in). Products where the account holds a role for this address come first
    /// ("institution": InstitutionAdmin or InstitutionOwner, "teacher": Teacher); when none fits, any staff product opens
    /// and the portal points to the right address. Among the candidates: the last used product, otherwise maxkoç.
    /// </summary>
    public static PlatformProduct? ResolveSignInProduct(User user, string? workspace, PlatformProduct? preferred)
    {
        var accesses = GetProductAccesses(user)
            .Where(access => access.Product is PlatformProduct.Coaching or PlatformProduct.SpeedReading)
            .ToArray();
        if (accesses.Length == 0)
        {
            return null;
        }

        var fitting = accesses.Where(access => FitsWorkspace(access, workspace)).ToArray();
        var candidates = fitting.Length > 0 ? fitting : accesses;
        if (preferred is { } last && candidates.Any(access => access.Product == last))
        {
            return last;
        }

        return candidates.Any(access => access.Product == PlatformProduct.Coaching)
            ? PlatformProduct.Coaching
            : candidates[0].Product;
    }

    public static EduPlatform.Shared.Kernel.Results.Error NotStaffAccount() => new(
        "Auth.NotStaffAccount",
        "Bu hesapta öğretmen veya kurum personeli erişimi bulunamadı.");

    private static bool FitsWorkspace(StaffPortalProductAccess access, string? workspace) =>
        workspace?.Trim().ToLowerInvariant() switch
        {
            "institution" => access.Roles.Any(role => string.Equals(role, "InstitutionAdmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "InstitutionOwner", StringComparison.OrdinalIgnoreCase)),
            "teacher" => access.Roles.Any(role => string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase)),
            _ => true
        };

    private static bool IsStaffRole(string roleName) =>
        string.Equals(roleName, "Teacher", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "InstitutionAdmin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "InstitutionOwner", StringComparison.OrdinalIgnoreCase);

    private static bool IsStaffPortalExcludedRole(string roleName) =>
        string.Equals(roleName, "SystemAdmin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "Editor", StringComparison.OrdinalIgnoreCase);
}
