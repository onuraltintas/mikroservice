namespace EduPlatform.Gateway.Configuration;

/// <summary>
/// Browser origins allowed to call the gateway cross-origin. Production frontends are
/// served from the same origin as <c>/api</c>, so the default is an empty list and
/// the CORS middleware is not registered at all.
/// </summary>
public sealed class GatewayCorsOptions
{
    public const string SectionName = "Gateway:Cors";
    public const string PolicyName = "GatewayFrontends";

    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Response headers the Angular clients read for pagination and badges.</summary>
    public string[] ExposedHeaders { get; set; } =
        ["X-Total-Count", "X-Unread-Count", "X-Page-Number", "X-Page-Size", "X-Correlation-ID"];

    /// <summary>Returns the validated origins; throws for wildcards, paths or non-HTTP(S) values.</summary>
    public string[] GetValidatedOrigins()
    {
        var origins = new List<string>();
        foreach (var origin in AllowedOrigins.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || origin.TrimEnd('/').Length != uri.GetLeftPart(UriPartial.Authority).Length)
            {
                throw new InvalidOperationException(
                    $"Gateway:Cors:AllowedOrigins must contain absolute http(s) origins without path, query or wildcard; '{origin}' is invalid.");
            }

            origins.Add(uri.GetLeftPart(UriPartial.Authority));
        }

        return origins.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
