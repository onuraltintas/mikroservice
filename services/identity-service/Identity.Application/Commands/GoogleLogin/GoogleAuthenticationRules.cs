using Identity.Application.Interfaces;
using Identity.Domain.Entities;

namespace Identity.Application.Commands.GoogleLogin;

internal static class GoogleAuthenticationRules
{
    public const string LoginProvider = "Google";

    private static readonly string[] TrustedIssuers =
    [
        "https://accounts.google.com",
        "accounts.google.com"
    ];

    public static bool IsVerifiedGoogleUser(GoogleUser? user) =>
        user is not null
        && !string.IsNullOrWhiteSpace(user.Email)
        && !string.IsNullOrWhiteSpace(user.GoogleId)
        && user.EmailVerified
        && TrustedIssuers.Contains(user.Issuer, StringComparer.Ordinal);

    public static bool RequiresExplicitLink(User user) => user.Roles.Any(userRole =>
        userRole.Role.Name.Equals("SystemAdmin", StringComparison.OrdinalIgnoreCase)
        || userRole.Role.Name.Equals("InstitutionAdmin", StringComparison.OrdinalIgnoreCase)
        || userRole.Role.Name.Equals("InstitutionOwner", StringComparison.OrdinalIgnoreCase)
        || userRole.Role.Name.Equals("Teacher", StringComparison.OrdinalIgnoreCase));
}
