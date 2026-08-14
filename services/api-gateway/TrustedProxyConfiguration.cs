using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using AspNetIPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace EduPlatform.Gateway;

/// <summary>
/// Builds forwarded-header options without trusting client-supplied proxy headers by default.
/// </summary>
public static class TrustedProxyConfiguration
{
    private const string SectionName = "ForwardedHeaders";
    private const int MaxForwardLimit = 10;

    public static ForwardedHeadersOptions Create(IConfiguration configuration)
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
        options.KnownNetworks.Clear();

        AddKnownProxies(options, ReadValues(section, "KnownProxies"));
        AddKnownNetworks(options, ReadValues(section, "KnownNetworks"));

        if (options.KnownProxies.Count > 0 || options.KnownNetworks.Count > 0)
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        }

        return options;
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
            if (!AspNetIPNetwork.TryParse(value.AsSpan(), out var network) || network.PrefixLength == 0)
            {
                throw new InvalidOperationException(
                    "ForwardedHeaders:KnownNetworks must contain a specific, non-catch-all network.");
            }

            options.KnownNetworks.Add(network);
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
