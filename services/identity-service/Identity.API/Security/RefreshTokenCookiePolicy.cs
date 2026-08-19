using Microsoft.AspNetCore.Http;

namespace Identity.API.Security;

public static class RefreshTokenCookiePolicy
{
    public const string CookieName = "eduplatform_refresh";

    public static CookieOptions CreateOptions(bool isProduction, DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        Secure = isProduction,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expiresAt,
        IsEssential = true
    };
}
