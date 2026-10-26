using Microsoft.Extensions.Configuration;

namespace Identity.Application.Services;

public static class PublicAppInvitationLink
{
    public static string Create(IConfiguration configuration, Guid invitationId)
    {
        var baseUrl = configuration["SpeedReadingPublicApp:BaseUrl"]?.TrimEnd('/')
            ?? configuration["PublicApp:BaseUrl"]?.TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("SpeedReadingPublicApp:BaseUrl must be a valid absolute HTTP(S) URL.");
        }

        var returnUrl = $"/auth/accept-invitation?id={invitationId}";
        return $"{baseUrl}/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }
}
