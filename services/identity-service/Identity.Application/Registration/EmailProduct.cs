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
}
