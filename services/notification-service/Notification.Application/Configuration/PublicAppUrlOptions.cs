using System.Net;

namespace Notification.Application.Configuration;

public sealed class PublicAppUrlOptions
{
    public const string SectionName = "PublicApp";

    public string BaseUrl { get; set; } = string.Empty;

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

    public string BuildEmailVerificationLink(Guid userId, string token)
    {
        if (!IsValidBaseUrl(BaseUrl))
        {
            throw new InvalidOperationException("PublicApp:BaseUrl is not a valid absolute HTTP(S) URL.");
        }

        var baseUrl = BaseUrl.TrimEnd('/');
        return $"{baseUrl}/auth/confirm-email?token={Uri.EscapeDataString(token)}&userId={Uri.EscapeDataString(userId.ToString())}";
    }

    public string BuildPasswordResetLink(string token, string email)
    {
        if (!IsValidBaseUrl(BaseUrl))
        {
            throw new InvalidOperationException("PublicApp:BaseUrl is not a valid absolute HTTP(S) URL.");
        }

        var baseUrl = BaseUrl.TrimEnd('/');
        return $"{baseUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";
    }
}
