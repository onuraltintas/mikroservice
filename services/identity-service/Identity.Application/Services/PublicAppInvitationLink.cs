using Microsoft.Extensions.Configuration;

namespace Identity.Application.Services;

public static class PublicAppInvitationLink
{
    public static string Create(IConfiguration configuration, Guid invitationId)
    {
        var baseUrl = configuration["PublicApp:CoachingBaseUrl"]?.TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("PublicApp:CoachingBaseUrl must be a valid absolute HTTP(S) URL.");
        }

        return $"{baseUrl}/coaching-portal/invitations?id={invitationId}";
    }
}
