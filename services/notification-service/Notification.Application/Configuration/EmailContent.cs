using System.Net;
using System.Text;

namespace Notification.Application.Configuration;

/// <summary>
/// Everything that comes from users (names, addresses, free-text messages) is inserted
/// into HTML e-mails only through these helpers, so it cannot add markup or links to a
/// message the platform sends from its own address.
/// </summary>
public static class EmailContent
{
    /// <summary>HTML-encodes text for element content and attribute values.</summary>
    public static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>Removes line breaks and control characters from text used in a subject.</summary>
    public static string SubjectText(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Returns <paramref name="link"/> when it is an absolute http(s) URL on one of the
    /// platform's own origins; otherwise <paramref name="fallback"/>. Links in e-mails are
    /// built by the services, so anything else means the event was tampered with.
    /// </summary>
    public static string PlatformLink(string? link, PublicAppUrlOptions origins, string fallback)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && string.IsNullOrEmpty(uri.UserInfo)
            && IsPlatformHost(uri, origins))
        {
            return uri.AbsoluteUri;
        }

        return fallback;
    }

    private static bool IsPlatformHost(Uri uri, PublicAppUrlOptions origins) =>
        new[] { origins.BaseUrl, origins.SpeedReadingBaseUrl }
            .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out _))
            .Select(origin => new Uri(origin))
            .Any(origin => string.Equals(origin.Host, uri.Host, StringComparison.OrdinalIgnoreCase)
                && origin.Port == uri.Port);
}
