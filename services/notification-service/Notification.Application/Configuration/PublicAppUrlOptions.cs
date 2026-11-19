using System.Net;

namespace Notification.Application.Configuration;

public sealed class PublicAppUrlOptions
{
    public const string SectionName = "PublicApp";

    /// <summary>The Coaching site (production https://maxkoc.com): Coaching student and staff links.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Alan adı planı (18.11.2026): the parents' own address (production https://veli.maxkoc.com), so a parent and a
    /// student can stay signed in in one browser. Empty means <see cref="BaseUrl"/>.
    /// </summary>
    public string ParentBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public origin for the standalone Master Hızlı Okuma application. The
    /// central Eduİvme origin remains the default for administrative roles.
    /// </summary>
    public string SpeedReadingBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Coaching audit K3: the coaching staff portal of teachers, e.g. https://ogretmen.eduivme.com/staff.
    /// Empty means "{BaseUrl}/staff".
    /// </summary>
    public string StaffPortalUrl { get; set; } = string.Empty;

    /// <summary>
    /// maxkampüs (19.11.2026): the Kurs Yönetimi site, production https://maxkampus.com. Account e-mails of the
    /// "course-management" product (verification, password reset, sign-in) open its own pages. Empty: older behaviour.
    /// </summary>
    public string CourseManagementBaseUrl { get; set; } = string.Empty;

    /// <summary>maxkampus.com pages (Turkish addresses, as on the site).</summary>
    public const string CourseManagementVerificationPath = "/eposta-dogrula";
    public const string CourseManagementResetPath = "/sifre-yenile";
    public const string CourseManagementLoginPath = "/giris";

    private bool IsCourseManagement(string? product) =>
        string.Equals(product, "course-management", StringComparison.OrdinalIgnoreCase) && IsValidBaseUrl(CourseManagementBaseUrl);

    /// <summary>Alan adı planı (18.11.2026): the staff portal of institution managers, e.g. https://kurum.eduivme.com/staff.
    /// Empty means <see cref="StaffPortalUrl"/>.</summary>
    public string InstitutionPortalUrl { get; set; } = string.Empty;

    /// <summary>Coaching audit K3: the admin panel origin, e.g. https://admin.eduivme.com. Empty means BaseUrl.
    /// Also the origin of links sent to management accounts (SystemAdmin, Editor).</summary>
    public string AdminPanelUrl { get; set; } = string.Empty;

    public string StaffPortalLink() =>
        $"{(IsValidBaseUrl(StaffPortalUrl) ? StaffPortalUrl.TrimEnd('/') : ResolveBaseUrl(null, "coaching") + "/staff")}/?product=coaching";

    public string InstitutionPortalLink() =>
        IsValidBaseUrl(InstitutionPortalUrl) ? $"{InstitutionPortalUrl.TrimEnd('/')}/?product=coaching" : StaffPortalLink();

    /// <summary>A Coaching page link: the parent panel (/coaching-portal/parent…) opens on the parents' address.</summary>
    public string CoachingLink(string path)
    {
        var relative = "/" + path.TrimStart('/');
        var parentPage = relative == "/coaching-portal/parent" || relative.StartsWith("/coaching-portal/parent/", StringComparison.Ordinal)
            || relative.StartsWith("/coaching-portal/parent?", StringComparison.Ordinal);
        return (parentPage && IsValidBaseUrl(ParentBaseUrl) ? ParentBaseUrl.TrimEnd('/') : ResolveBaseUrl(null, "coaching")) + relative;
    }

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

    /// <summary>The roles of the Coaching and speed-reading products ("User" = an account without a role).</summary>
    private static readonly HashSet<string> ProductRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Student",
        "Teacher",
        "Parent",
        "InstitutionAdmin",
        "InstitutionOwner",
        "User"
    };

    /// <summary>
    /// Management accounts (SystemAdmin, Editor, course-management and any role made in role management): their
    /// links open the admin panel (<see cref="AdminPanelUrl"/>), never the Coaching site, whose /dashboard is closed.
    /// </summary>
    public static bool IsManagementRole(string? role) =>
        !string.IsNullOrWhiteSpace(role) && !ProductRoles.Contains(role.Trim());

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
        product is null || product.ToLowerInvariant() is "coaching" or "speed-reading" or "course-management";

    /// <summary>The admin-panel (Coaching site) page that reads <c>userId</c> and <c>token</c>.</summary>
    public const string CoachingEmailVerificationPath = "/auth/confirm-email";

    /// <summary>The speed-reading app page that reads <c>token</c> and <c>userId</c>.</summary>
    public const string SpeedReadingEmailVerificationPath = "/auth/verify-email";

    /// <summary>The reset page of every site (admin-panel and speed-reading); it reads <c>token</c> and <c>email</c>.</summary>
    public const string PasswordResetPath = "/auth/reset-password";

    /// <summary>E5 (#22): the admin-panel page (Coaching site, or the admin panel for management) that reads
    /// <c>userId</c> and <c>token</c> of an e-mail change.</summary>
    public const string EmailChangeConfirmationPath = "/auth/confirm-email-change";

    public string ResolveBaseUrl(string? role = null) => ResolveBaseUrl(role, null);

    /// <summary>
    /// User decision 05.11.2026: <see cref="BaseUrl"/> is the Coaching site (production https://maxkoc.com)
    /// for students and Coaching staff; parents use <see cref="ParentBaseUrl"/> when it is set (alan adı planı
    /// 18.11.2026); management accounts use <see cref="AdminPanelUrl"/> when it is set; speed-reading links use
    /// <see cref="SpeedReadingBaseUrl"/>.
    /// </summary>
    public string ResolveBaseUrl(string? role, string? product)
    {
        if (IsCourseManagement(product)) return CourseManagementBaseUrl.TrimEnd('/');
        var candidate = IsManagementRole(role) && IsValidBaseUrl(AdminPanelUrl)
            ? AdminPanelUrl
            : IsSpeedReadingProduct(role, product) && IsValidBaseUrl(SpeedReadingBaseUrl)
            ? SpeedReadingBaseUrl
            : string.Equals(role?.Trim(), "Parent", StringComparison.OrdinalIgnoreCase) && IsValidBaseUrl(ParentBaseUrl)
            ? ParentBaseUrl
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
        if (IsCourseManagement(product)) return $"{baseUrl}{CourseManagementVerificationPath}?userId={id}&token={value}";
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
        if (IsCourseManagement(product))
            return $"{baseUrl}{CourseManagementResetPath}?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";
        return $"{baseUrl}{PasswordResetPath}?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";
    }

    /// <summary>
    /// E5 (#22): the e-mail change link always opens an admin-panel origin — the Coaching site, or the admin panel for
    /// a management role — because the speed-reading app has no such page.
    /// </summary>
    public string BuildEmailChangeLink(Guid userId, string token, string? role) =>
        $"{ResolveBaseUrl(role, "coaching")}{EmailChangeConfirmationPath}?userId={Uri.EscapeDataString(userId.ToString())}&token={Uri.EscapeDataString(token)}";

    /// <summary>E5 (#22): the support page of the same origin (told in the notice to the previous address).</summary>
    public string BuildSupportLink(string? role) => $"{ResolveBaseUrl(role, "coaching")}/auth/support";

    public string BuildLoginLink(string? role = null) => BuildLoginLink(role, null);

    public string BuildLoginLink(string? role, string? product) =>
        $"{ResolveBaseUrl(role, product)}{(IsCourseManagement(product) ? CourseManagementLoginPath : "/auth/login")}";

    public static string GetApplicationName(string? role) =>
        GetApplicationName(role, null);

    public static string GetApplicationName(string? role, string? product) =>
        string.Equals(product, "course-management", StringComparison.OrdinalIgnoreCase) ? "maxkampüs"
        : IsSpeedReadingProduct(role, product) ? "Master Hızlı Okuma" : IsLearnerRole(role) ? "maxkoç" : "eduivme";

    /// <summary>Brand decision 18.11.2026: Koçluk students and parents use maxkoç; teachers, institutions and managers eduivme.</summary>
    private static bool IsLearnerRole(string? role) =>
        string.Equals(role?.Trim(), "Student", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role?.Trim(), "Parent", StringComparison.OrdinalIgnoreCase);

    public static string GetTemplateName(string baseName, string? role) =>
        GetTemplateName(baseName, role, null);

    public static string GetTemplateName(string baseName, string? role, string? product) =>
        IsSpeedReadingProduct(role, product) ? $"SpeedReading_{baseName}" : baseName;

    public static string ApplyBranding(string value, string? role) => ApplyBranding(value, role, null);

    /// <summary>Stored templates say "Eduİvme"; the recipient sees the name of the product they use.</summary>
    public static string ApplyBranding(string value, string? role, string? product)
    {
        if (!IsSpeedReadingProduct(role, product))
        {
            var name = GetApplicationName(role, product);
            return value
                .Replace("Eduİvme Koçluk", name, StringComparison.Ordinal)
                .Replace("Eduİvme", name, StringComparison.Ordinal);
        }

        return value
            .Replace("EduPlatform INC.", "ONAL Yazılım ve Otomasyon", StringComparison.Ordinal)
            .Replace("EduPlatform", "Master Hızlı Okuma", StringComparison.Ordinal)
            .Replace("Eduİvme", "Master Hızlı Okuma", StringComparison.Ordinal);
    }
}
