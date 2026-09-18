using System.Net;

namespace Notification.Application.Configuration;

public sealed class PublicAppUrlOptions
{
    public const string SectionName = "PublicApp";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public origin for the standalone Master Hızlı Okuma application. The
    /// central Eduİvme origin remains the default for administrative roles.
    /// </summary>
    public string SpeedReadingBaseUrl { get; set; } = string.Empty;

    private static readonly HashSet<string> SpeedReadingRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Student",
        "Teacher",
        "Parent",
        "InstitutionAdmin",
        "InstitutionOwner"
    };

    public static bool IsValidBaseUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && !string.IsNullOrWhiteSpace(uri.Host);
    }

    public static bool IsValidForEnvironment(string? value, bool isProduction)
    {
        if (!IsValidBaseUrl(value))
        {
            return false;
        }

        if (!isProduction || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return true;
        }

        return !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && (!IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address));
    }

    public static bool IsSpeedReadingRole(string? role) =>
        !string.IsNullOrWhiteSpace(role) && SpeedReadingRoles.Contains(role.Trim());

    public string ResolveBaseUrl(string? role = null)
    {
        var candidate = IsSpeedReadingRole(role) && IsValidBaseUrl(SpeedReadingBaseUrl)
            ? SpeedReadingBaseUrl
            : BaseUrl;

        if (!IsValidBaseUrl(candidate))
        {
            throw new InvalidOperationException("PublicApp:BaseUrl is not a valid absolute HTTP(S) URL.");
        }

        return candidate.TrimEnd('/');
    }

    public string BuildEmailVerificationLink(Guid userId, string token, string? role = null)
    {
        var baseUrl = ResolveBaseUrl(role);
        return $"{baseUrl}/auth/verify-email?token={Uri.EscapeDataString(token)}&userId={Uri.EscapeDataString(userId.ToString())}";
    }

    public string BuildPasswordResetLink(string token, string email, string? role = null)
    {
        var baseUrl = ResolveBaseUrl(role);
        return $"{baseUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";
    }

    public string BuildLoginLink(string? role = null) => $"{ResolveBaseUrl(role)}/auth/login";

    public static string GetApplicationName(string? role) =>
        IsSpeedReadingRole(role) ? "Master Hızlı Okuma" : "Eduİvme";

    public static string GetCompanyName(string? role) =>
        IsSpeedReadingRole(role) ? "ONAL Yazılım ve Otomasyon" : "Eduİvme";

    public static string GetTemplateName(string baseName, string? role) =>
        IsSpeedReadingRole(role) ? $"SpeedReading_{baseName}" : baseName;

    public static string ApplySpeedReadingBranding(string value, string? role)
    {
        if (!IsSpeedReadingRole(role))
        {
            return value;
        }

        return value
            .Replace("EduPlatform INC.", "ONAL Yazılım ve Otomasyon", StringComparison.Ordinal)
            .Replace("EduPlatform", "Master Hızlı Okuma", StringComparison.Ordinal)
            .Replace("Eduİvme", "Master Hızlı Okuma", StringComparison.Ordinal);
    }
}
