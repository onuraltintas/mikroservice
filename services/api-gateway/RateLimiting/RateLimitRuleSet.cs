namespace EduPlatform.Gateway.RateLimiting;

/// <summary>Ordered, first-match lookup over the configured route rules.</summary>
public sealed class RateLimitRuleSet
{
    private readonly IReadOnlyList<GatewayRateLimitRule> _rules;

    public RateLimitRuleSet(IEnumerable<GatewayRateLimitRule> rules) => _rules = rules.ToArray();

    public GatewayRateLimitRule? Match(string method, PathString path)
    {
        var pathValue = Normalize(path.Value);
        foreach (var rule in _rules)
        {
            if (rule.Methods.Length > 0
                && !rule.Methods.Any(allowed => string.Equals(allowed, method, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (rule.Paths.Any(candidate => string.Equals(Normalize(candidate), pathValue, StringComparison.OrdinalIgnoreCase))
                || rule.PathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return rule;
            }
        }

        return null;
    }

    private static string Normalize(string? path) =>
        string.IsNullOrEmpty(path) ? "/" : path.Length > 1 ? path.TrimEnd('/') : path;
}
