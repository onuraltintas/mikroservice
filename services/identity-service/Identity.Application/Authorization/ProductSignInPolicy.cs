using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Authorization;

/// <summary>
/// Paket F (07.11.2026, F4): a product session (password, Google, refresh) needs at least one live role in that
/// product's scope, not only an active <see cref="UserProductAccess"/> row. The scope includes the global
/// SystemAdmin role, so SystemAdmin stays exempt. Product access and roles are managed separately in the admin
/// panel (Rol Yönetimi vs. Kullanıcı Düzenle), so removing a user's last role in a product leaves the access row
/// active; this rule keeps such a role-less account from getting an empty, product-scoped token.
/// </summary>
public static class ProductSignInPolicy
{
    public const string NoRoleForProductCode = "Auth.NoRoleForProduct";

    /// <summary>
    /// True when the account holds a role in <paramref name="product"/>'s scope whose role is not soft-deleted.
    /// A membership whose <see cref="Role"/> is not loaded (just added in memory) counts as live.
    /// </summary>
    public static bool HasLiveRoleInProduct(User user, PlatformProduct product) =>
        user.GetRolesForProductScope(product).Any(userRole => userRole.Role is null || !userRole.Role.IsDeleted);

    public static Error NoRoleForProduct() => new(
        NoRoleForProductCode,
        "Bu platformda hesabınıza tanımlı aktif bir rol yok. Mevcut hesabınızla yeniden katılmak için kayıt sayfasındaki \"Mevcut hesabımla katıl\" seçeneğini kullanabilirsiniz.");
}
