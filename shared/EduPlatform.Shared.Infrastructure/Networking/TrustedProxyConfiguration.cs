using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using SystemNetIPNetwork = System.Net.IPNetwork;

namespace EduPlatform.Shared.Infrastructure.Networking;

/// <summary>
/// Builds forwarded-header options without trusting client-supplied proxy headers by default.
/// Only explicitly configured proxy addresses or networks are trusted; an empty configuration
/// leaves the request untouched so services keep their direct connection address.
/// </summary>
public static class TrustedProxyConfiguration
{
    public const string SectionName = "ForwardedHeaders";
    private const int MaxForwardLimit = 10;

    /// <summary>
    /// Creates options that trust X-Forwarded-For and X-Forwarded-Proto from configured proxies.
    /// </summary>
    public static ForwardedHeadersOptions Create(IConfiguration configuration) =>
        Create(configuration, ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

    /// <summary>
    /// Creates options that trust only <paramref name="trustedHeaders"/> from configured proxies.
    /// </summary>
    public static ForwardedHeadersOptions Create(IConfiguration configuration, ForwardedHeaders trustedHeaders)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var forwardLimit = section.GetValue<int?>("ForwardLimit") ?? 1;
        if (forwardLimit is < 1 or > MaxForwardLimit)
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:ForwardLimit must be between 1 and {MaxForwardLimit}.");
        }

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.None,
            ForwardLimit = forwardLimit
        };

        // Clear framework defaults so only explicitly configured ingress addresses are trusted.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        AddKnownProxies(options, ReadValues(section, "KnownProxies"));
        AddKnownNetworks(options, ReadValues(section, "KnownNetworks"));

        if (options.KnownProxies.Count > 0 || options.KnownIPNetworks.Count > 0)
        {
            options.ForwardedHeaders = trustedHeaders;
        }

        return options;
    }

    /// <summary>
    /// Applies <see cref="Create(IConfiguration, ForwardedHeaders)"/> as the first request middleware.
    /// With no configured trusted proxy this is a no-op.
    /// </summary>
    public static WebApplication UseTrustedForwardedHeaders(
        this WebApplication app,
        ForwardedHeaders trustedHeaders)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseForwardedHeaders(Create(app.Configuration, trustedHeaders));
        return app;
    }

    private static void AddKnownProxies(ForwardedHeadersOptions options, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException("ForwardedHeaders:KnownProxies contains an invalid IP address.");
            }

            options.KnownProxies.Add(address);
        }
    }

    private static void AddKnownNetworks(ForwardedHeadersOptions options, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (!SystemNetIPNetwork.TryParse(value, out var network) || network.PrefixLength == 0)
            {
                throw new InvalidOperationException(
                    "ForwardedHeaders:KnownNetworks must contain a specific, non-catch-all network.");
            }

            options.KnownIPNetworks.Add(network);
        }
    }

    private static IEnumerable<string> ReadValues(IConfiguration section, string key)
    {
        var indexedValues = section.GetSection(key)
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var directValue = section[key];
        var directValues = string.IsNullOrWhiteSpace(directValue)
            ? Enumerable.Empty<string>()
            : directValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return indexedValues.Concat(directValues).Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
