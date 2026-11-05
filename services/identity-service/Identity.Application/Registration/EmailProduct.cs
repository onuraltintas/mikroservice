using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Registration;

/// <summary>
/// The product an account e-mail (verification, welcome) is for: the product the asking site names when the
/// account has it, else the account's only Coaching/Speed-reading product. Null (both or neither) lets the
/// notification service decide by role, as for older messages.
/// </summary>
public static class EmailProduct
{
    public static string? Resolve(User user, string? requested)
    {
        if (PlatformProductExtensions.TryParseRouteValue(requested, out var product)
            && product is PlatformProduct.Coaching or PlatformProduct.SpeedReading
            && user.HasProductAccess(product))
            return product.ToRouteValue();
        var products = new[] { PlatformProduct.Coaching, PlatformProduct.SpeedReading }.Where(user.HasProductAccess).ToArray();
        return products.Length == 1 ? products[0].ToRouteValue() : null;
    }

    private static readonly string[] ProductRoleNames =
    [
        Identity.Domain.Enums.UserRole.Student.ToString(),
        Identity.Domain.Enums.UserRole.Teacher.ToString(),
        Identity.Domain.Enums.UserRole.Parent.ToString(),
        Identity.Domain.Enums.UserRole.InstitutionAdmin.ToString(),
        Identity.Domain.Enums.UserRole.InstitutionOwner.ToString()
    ];

    /// <summary>
    /// Reviewer L1: the role named on the e-mail. With a product, the account's product role there (so a custom
    /// role next to it does not move the links to the admin panel); otherwise, or without one, the alphabetically
    /// first role as before, or <paramref name="fallback"/>.
    /// </summary>
    public static string Role(User user, string? product, string fallback)
    {
        if (PlatformProductExtensions.TryParseRouteValue(product, out var scope))
        {
            var productRole = user.Roles
                .Where(role => role.Product == scope && role.Role is { IsDeleted: false } && ProductRoleNames.Contains(role.Role.Name))
                .Select(role => role.Role.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .FirstOrDefault();
            if (productRole is not null) return productRole;
        }
        return user.Roles
            .Where(role => role.Role is not null)
            .OrderBy(role => role.Role.Name)
            .Select(role => role.Role.Name)
            .FirstOrDefault() ?? fallback;
    }
}
