using Microsoft.AspNetCore.Http;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.RefreshToken;

namespace Identity.API.Security;

public static class RefreshTokenCookiePolicy
{
    public const string CookieName = "eduplatform_refresh";
    public const string CourseManagementCookieName = "eduplatform_course_management_refresh";

    public static CookieOptions CreateOptions(
        bool isProduction,
        DateTimeOffset expiresAt,
        bool isPersistent = true) => new()
    {
        HttpOnly = true,
        Secure = isProduction,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = isPersistent ? expiresAt : null,
        IsEssential = true
    };

    public static CookieOptions CreateCourseManagementOptions(
        bool isProduction,
        DateTimeOffset expiresAt,
        bool isPersistent = true)
    {
        var options = CreateOptions(isProduction, expiresAt, isPersistent);
        options.Path = "/api/auth/course-management";
        return options;
    }

    public static AuthSessionResponse Issue(
        HttpResponse response,
        LoginResponse session,
        bool isProduction) => Issue(
            response,
            session.AccessToken ?? throw new InvalidOperationException("An access token is required to issue a session."),
            session.RefreshToken ?? throw new InvalidOperationException("A refresh token is required to issue a session."),
            session.RefreshTokenExpiresAt ?? throw new InvalidOperationException("A refresh token expiry is required to issue a session."),
            session.TokenType,
            session.ExpiresInMinutes,
            session.IsPersistent,
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
            session.IsPersistent,
            isProduction);

    public static void Clear(HttpResponse response, bool isProduction)
    {
        response.Cookies.Delete(CookieName, CreateOptions(isProduction, DateTimeOffset.UnixEpoch));
    }

    public static AuthSessionResponse IssueCourseManagement(
        HttpResponse response,
        LoginResponse session,
        bool isProduction) => Issue(
            response,
            session.AccessToken ?? throw new InvalidOperationException("An access token is required to issue a session."),
            session.RefreshToken ?? throw new InvalidOperationException("A refresh token is required to issue a session."),
            session.RefreshTokenExpiresAt ?? throw new InvalidOperationException("A refresh token expiry is required to issue a session."),
            session.TokenType,
            session.ExpiresInMinutes,
            session.IsPersistent,
            CourseManagementCookieName,
            CreateCourseManagementOptions(isProduction, new DateTimeOffset(session.RefreshTokenExpiresAt.Value), session.IsPersistent));

    public static AuthSessionResponse IssueCourseManagement(
        HttpResponse response,
        RefreshTokenResponse session,
        bool isProduction) => Issue(
            response,
            session.AccessToken,
            session.RefreshToken,
            session.RefreshTokenExpiresAt,
            session.TokenType,
            session.ExpiresInMinutes,
            session.IsPersistent,
            CourseManagementCookieName,
            CreateCourseManagementOptions(isProduction, new DateTimeOffset(session.RefreshTokenExpiresAt), session.IsPersistent));

    public static void ClearCourseManagement(HttpResponse response, bool isProduction)
    {
        response.Cookies.Delete(
            CourseManagementCookieName,
            CreateCourseManagementOptions(isProduction, DateTimeOffset.UnixEpoch));
    }

    private static AuthSessionResponse Issue(
        HttpResponse response,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAt,
        string tokenType,
        int expiresInMinutes,
        bool isPersistent,
        bool isProduction) => Issue(
            response,
            accessToken,
            refreshToken,
            refreshTokenExpiresAt,
            tokenType,
            expiresInMinutes,
            isPersistent,
            CookieName,
            CreateOptions(isProduction, new DateTimeOffset(refreshTokenExpiresAt), isPersistent));

    private static AuthSessionResponse Issue(
        HttpResponse response,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAt,
        string tokenType,
        int expiresInMinutes,
        bool isPersistent,
        string cookieName,
        CookieOptions cookieOptions)
    {
        response.Cookies.Append(
            cookieName,
            refreshToken,
            cookieOptions);

        return new AuthSessionResponse(accessToken, tokenType, expiresInMinutes);
    }
}

public sealed record AuthSessionResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInMinutes);
