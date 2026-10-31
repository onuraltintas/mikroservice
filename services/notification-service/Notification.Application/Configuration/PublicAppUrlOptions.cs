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

    /// <summary>
    /// Coaching audit K3: the coaching staff portal (teachers and institutions), e.g. https://onuraltintas.net/staff.
    /// Empty means "{BaseUrl}/staff".
    /// </summary>
    public string StaffPortalUrl { get; set; } = string.Empty;

    /// <summary>Coaching audit K3: the admin panel origin, e.g. https://eduivme.com. Empty means BaseUrl.</summary>
    public string AdminPanelUrl { get; set; } = string.Empty;

    public string StaffPortalLink() =>
        $"{(IsValidBaseUrl(StaffPortalUrl) ? StaffPortalUrl.TrimEnd('/') : ResolveBaseUrl(null, "coaching") + "/staff")}/?product=coaching";

    public string AdminPanelLink(string path) =>
        (IsValidBaseUrl(AdminPanelUrl) ? AdminPanelUrl.TrimEnd('/') : ResolveBaseUrl(null, "coaching")) + "/" + path.TrimStart('/');

    /// <summary>Optional addresses: empty, or valid for the environment.</summary>
    public static bool IsValidOptionalForEnvironment(string? value, bool isProduction) =>
        string.IsNullOrWhiteSpace(value) || IsValidForEnvironment(value, isProduction);

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

        // Coaching audit K3: links in production e-mails must be https (Coaching:PublicBaseUrl already requires it).
        return uri.Scheme == Uri.UriSchemeHttps
            && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && (!IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address));
    }

    public static bool IsSpeedReadingRole(string? role) =>
        !string.IsNullOrWhiteSpace(role) && SpeedReadingRoles.Contains(role.Trim());

    private static bool IsSpeedReadingProduct(string? role, string? product) => product?.ToLowerInvariant() switch
    {
        null => IsSpeedReadingRole(role),
        "coaching" => false,
        "speed-reading" => true,
        _ => throw new InvalidOperationException("Unknown notification product.")
    };

    public string ResolveBaseUrl(string? role = null) => ResolveBaseUrl(role, null);

    public string ResolveBaseUrl(string? role, string? product)
    {
        var candidate = IsSpeedReadingProduct(role, product) && IsValidBaseUrl(SpeedReadingBaseUrl)
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

    public string BuildPasswordResetLink(string token, string email, string? role = null) =>
        BuildPasswordResetLink(token, email, role, null);

    public string BuildPasswordResetLink(string token, string email, string? role, string? product)
    {
        var baseUrl = ResolveBaseUrl(role, product);
        return $"{baseUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";
    }

    public string BuildLoginLink(string? role = null) => $"{ResolveBaseUrl(role)}/auth/login";

    public static string GetApplicationName(string? role) =>
        GetApplicationName(role, null);

    public static string GetApplicationName(string? role, string? product) =>
        IsSpeedReadingProduct(role, product) ? "Master Hızlı Okuma" : "Eduİvme";

    public static string GetTemplateName(string baseName, string? role) =>
        GetTemplateName(baseName, role, null);

    public static string GetTemplateName(string baseName, string? role, string? product) =>
        IsSpeedReadingProduct(role, product) ? $"SpeedReading_{baseName}" : baseName;

    public static string ApplySpeedReadingBranding(string value, string? role) => ApplySpeedReadingBranding(value, role, null);

    public static string ApplySpeedReadingBranding(string value, string? role, string? product)
    {
        if (!IsSpeedReadingProduct(role, product))
        {
            return value;
        }

        return value
            .Replace("EduPlatform INC.", "ONAL Yazılım ve Otomasyon", StringComparison.Ordinal)
            .Replace("EduPlatform", "Master Hızlı Okuma", StringComparison.Ordinal)
            .Replace("Eduİvme", "Master Hızlı Okuma", StringComparison.Ordinal);
    }
}
