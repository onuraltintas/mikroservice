using Microsoft.Extensions.Configuration;

namespace Coaching.Infrastructure;

/// <summary>
/// Roadmap D3: the public site address used in coaching links (invitations, newsletter). <c>Coaching:PublicBaseUrl</c>
/// is the setting; the older <c>CoachingNewsletter:PublicBaseUrl</c> is read when it is missing. Production and staging
/// must set an https address (checked at startup); elsewhere the live site is the default.
/// </summary>
public static class CoachingPublicUrl
{
    public const string Default = "https://eduivme.com";

    public static string Get(IConfiguration configuration) => Configured(configuration) ?? Default;

    public static string Require(IConfiguration configuration, bool requireConfigured)
    {
        var value = Configured(configuration);
        if (value is null)
        {
            if (requireConfigured) throw new InvalidOperationException("Coaching:PublicBaseUrl must be set (for example https://eduivme.com).");
            return Default;
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")
            || requireConfigured && uri.Scheme != "https")
            throw new InvalidOperationException("Coaching:PublicBaseUrl must be an absolute https address.");
        return value;
    }

    /// <summary>
    /// Coaching audit K3: the coaching staff portal for teachers and institutions. It is served on its own origin
    /// (onuraltintas.net/staff), so it has its own setting; without it the portal is assumed under the public address.
    /// </summary>
    public static string StaffPortal(IConfiguration configuration)
    {
        var value = configuration["Coaching:StaffPortalUrl"];
        if (string.IsNullOrWhiteSpace(value)) return Get(configuration) + "/staff";
        value = value.Trim().TrimEnd('/');
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Coaching:StaffPortalUrl must be an absolute http(s) address.");
        return value;
    }

    public static string StaffPortalLink(IConfiguration configuration) => StaffPortal(configuration) + "/?product=coaching";

    private static string? Configured(IConfiguration configuration)
    {
        var value = configuration["Coaching:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(value)) value = configuration["CoachingNewsletter:PublicBaseUrl"];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
    }
}
