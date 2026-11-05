using System.Net;

namespace Notification.Application.Configuration;

public sealed class PublicAppUrlOptions
{
    public const string SectionName = "PublicApp";

    /// <summary>The Coaching site (production https://onuraltintas.net): Coaching and parent links.</summary>
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

    /// <summary>Coaching audit K3: the admin panel origin, e.g. https://eduivme.com. Empty means BaseUrl.
    /// Also the origin of links sent to management accounts (SystemAdmin, Editor).</summary>
    public string AdminPanelUrl { get; set; } = string.Empty;

    public string StaffPortalLink() =>
        $"{(IsValidBaseUrl(StaffPortalUrl) ? StaffPortalUrl.TrimEnd('/') : ResolveBaseUrl(null, "coaching") + "/staff")}/?product=coaching";

    public string AdminPanelLink(string path) =>
        (IsValidBaseUrl(AdminPanelUrl) ? AdminPanelUrl.TrimEnd('/') : ResolveBaseUrl(null, "coaching")) + "/" + path.TrimStart('/');

    /// <summary>Optional addresses: empty, or valid for the environment.</summary>
    public static bool IsValidOptionalForEnvironment(string? value, bool isProduction) =>
        string.IsNullOrWhiteSpace(value) || IsValidForEnvironment(value, isProduction);

    // Parents exist only in Coaching, so a Parent message without a product goes to the Coaching site.
    private static readonly HashSet<string> SpeedReadingRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Student",
        "Teacher",
        "InstitutionAdmin",
        "InstitutionOwner"
    };

    /// <summary>Management accounts: their links open the admin panel (<see cref="AdminPanelUrl"/>), never the Coaching site.</summary>
    private static readonly HashSet<string> ManagementRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "SystemAdmin",
        "Admin",
        "Editor"
    };

    public static bool IsManagementRole(string? role) =>
        !string.IsNullOrWhiteSpace(role) && ManagementRoles.Contains(role.Trim());

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

    /// <summary>Second review L5: an unknown product falls back to the role (as messages without one); consumers log it.</summary>
    private static bool IsSpeedReadingProduct(string? role, string? product) => product?.ToLowerInvariant() switch
    {
        "coaching" => false,
        "speed-reading" => true,
        _ => IsSpeedReadingRole(role)
    };

    /// <summary>Null (older message shape), "coaching" or "speed-reading".</summary>
    public static bool IsKnownProduct(string? product) =>
        product is null || product.ToLowerInvariant() is "coaching" or "speed-reading";

    /// <summary>The admin-panel (Coaching site) page that reads <c>userId</c> and <c>token</c>.</summary>
    public const string CoachingEmailVerificationPath = "/auth/confirm-email";

    /// <summary>The speed-reading app page that reads <c>token</c> and <c>userId</c>.</summary>
    public const string SpeedReadingEmailVerificationPath = "/auth/verify-email";

    public string ResolveBaseUrl(string? role = null) => ResolveBaseUrl(role, null);

    /// <summary>
    /// User decision 05.11.2026: <see cref="BaseUrl"/> is the Coaching site (production https://onuraltintas.net)
    /// for students, parents and Coaching staff; management accounts use <see cref="AdminPanelUrl"/> when it is set;
    /// speed-reading links use <see cref="SpeedReadingBaseUrl"/>.
    /// </summary>
    public string ResolveBaseUrl(string? role, string? product)
    {
        var candidate = IsManagementRole(role) && IsValidBaseUrl(AdminPanelUrl)
            ? AdminPanelUrl
            : IsSpeedReadingProduct(role, product) && IsValidBaseUrl(SpeedReadingBaseUrl)
            ? SpeedReadingBaseUrl
            : BaseUrl;

        if (!IsValidBaseUrl(candidate))
        {
            throw new InvalidOperationException("PublicApp:BaseUrl is not a valid absolute HTTP(S) URL.");
        }

        return candidate.TrimEnd('/');
    }

    public string BuildEmailVerificationLink(Guid userId, string token, string? role = null) =>
        BuildEmailVerificationLink(userId, token, role, null);

    public string BuildEmailVerificationLink(Guid userId, string token, string? role, string? product)
    {
        var baseUrl = ResolveBaseUrl(role, product);
        var id = Uri.EscapeDataString(userId.ToString());
        var value = Uri.EscapeDataString(token);
        // PR #111 second review H1: each site's own verification page; the Coaching site has no /auth/verify-email.
        return !IsManagementRole(role) && IsSpeedReadingProduct(role, product) && IsValidBaseUrl(SpeedReadingBaseUrl)
            ? $"{baseUrl}{SpeedReadingEmailVerificationPath}?token={value}&userId={id}"
            : $"{baseUrl}{CoachingEmailVerificationPath}?userId={id}&token={value}";
    }

    public string BuildPasswordResetLink(string token, string email, string? role = null) =>
        BuildPasswordResetLink(token, email, role, null);

    public string BuildPasswordResetLink(string token, string email, string? role, string? product)
    {
        var baseUrl = ResolveBaseUrl(role, product);
        return $"{baseUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";
    }

    public string BuildLoginLink(string? role = null) => BuildLoginLink(role, null);

    public string BuildLoginLink(string? role, string? product) => $"{ResolveBaseUrl(role, product)}/auth/login";

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
