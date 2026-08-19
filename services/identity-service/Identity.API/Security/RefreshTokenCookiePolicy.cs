using Microsoft.AspNetCore.Http;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.RefreshToken;

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

    public static AuthSessionResponse Issue(
        HttpResponse response,
        LoginResponse session,
        bool isProduction) => Issue(
            response,
            session.AccessToken,
            session.RefreshToken,
            session.RefreshTokenExpiresAt,
            session.TokenType,
            session.ExpiresInMinutes,
            isProduction);

    public static AuthSessionResponse Issue(
        HttpResponse response,
        RefreshTokenResponse session,
        bool isProduction) => Issue(
            response,
            session.AccessToken,
            session.RefreshToken,
            session.RefreshTokenExpiresAt,
            session.TokenType,
            session.ExpiresInMinutes,
            isProduction);

    public static void Clear(HttpResponse response, bool isProduction)
    {
        response.Cookies.Delete(CookieName, CreateOptions(isProduction, DateTimeOffset.UnixEpoch));
    }

    private static AuthSessionResponse Issue(
        HttpResponse response,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAt,
        string tokenType,
        int expiresInMinutes,
        bool isProduction)
    {
        response.Cookies.Append(
            CookieName,
            refreshToken,
            CreateOptions(isProduction, new DateTimeOffset(refreshTokenExpiresAt)));

        return new AuthSessionResponse(accessToken, tokenType, expiresInMinutes);
    }
}

public sealed record AuthSessionResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInMinutes);
