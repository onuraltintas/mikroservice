using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Authorization;

public enum ProductSignInDecision
{
    Allowed,
    AccessDenied,
    NoRoleForProduct
}

/// <summary>
/// Paket F (07.11.2026, F4): the one rule for every product session (password, Google, refresh, MFA completion):
/// SystemAdmin, or an active access to the product and at least one live role in it. A live role is a membership
/// whose role is loaded and not soft-deleted (fail closed). Removing a user's last role in a product also closes
/// that product's access (user decision 07.11.2026), so this mostly guards legacy rows and unexpected states.
/// </summary>
public static class ProductSignInPolicy
{
    public const string NoRoleForProductCode = "Auth.NoRoleForProduct";

    public static ProductSignInDecision Evaluate(User user, PlatformProduct? product)
    {
        if (IsSystemAdministrator(user))
            return ProductSignInDecision.Allowed;
        if (product is not { } requestedProduct || !user.HasProductAccess(requestedProduct))
            return ProductSignInDecision.AccessDenied;
        return HasLiveRoleInProduct(user, requestedProduct)
            ? ProductSignInDecision.Allowed
            : ProductSignInDecision.NoRoleForProduct;
    }

    /// <summary>The account holds the global SystemAdmin role (loaded, not deleted).</summary>
    public static bool IsSystemAdministrator(User user) =>
        user.Roles.Any(userRole => userRole.Product is null && IsLive(userRole)
            && string.Equals(userRole.Role.Name, Identity.Domain.Enums.UserRole.SystemAdmin.ToString(), StringComparison.OrdinalIgnoreCase));

    /// <summary>True when the account holds a live role in <paramref name="product"/>'s scope.</summary>
    public static bool HasLiveRoleInProduct(User user, PlatformProduct product) =>
        user.GetRolesForProductScope(product).Any(IsLive);

    /// <summary>True when the account holds a live role assigned to exactly <paramref name="product"/>.</summary>
    public static bool HasLiveProductScopedRole(User user, PlatformProduct product) =>
        user.Roles.Any(userRole => userRole.Product == product && IsLive(userRole));

    public static Error NoRoleForProduct() => new(
        NoRoleForProductCode,
        "Bu platformda hesabınıza tanımlı bir yetki bulunmuyor. Yeniden erişim için kurumunuzla ya da destekle iletişime geçin.");

    public static Error AccessDenied() => new(
        "Auth.ProductAccessDenied",
        "Bu platform için hesabınıza erişim yetkisi tanımlı değil.");

    private static bool IsLive(Identity.Domain.Entities.UserRole userRole) =>
        userRole.Role is not null && !userRole.Role.IsDeleted;
}
