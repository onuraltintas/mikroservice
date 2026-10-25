namespace EduPlatform.Gateway.Configuration;

/// <summary>
/// Request body limits (section <c>Gateway:RequestLimits</c>). Services keep their own,
/// stricter per-endpoint limits; the gateway rejects clearly oversized bodies early.
/// </summary>
public sealed class GatewayRequestLimitOptions
{
    public const string SectionName = "Gateway:RequestLimits";

    /// <summary>Kestrel's hard ceiling; must cover the largest rule.</summary>
    public long MaxRequestBodySizeBytes { get; set; } = 52_428_800;

    /// <summary>Limit for requests no rule matches (Kestrel's former default).</summary>
    public long DefaultMaxRequestBodySizeBytes { get; set; } = 30_000_000;

    public List<GatewayRequestLimitRule> Rules { get; set; } = [];

    public void Validate()
    {
        if (DefaultMaxRequestBodySizeBytes < 1 || DefaultMaxRequestBodySizeBytes > MaxRequestBodySizeBytes)
        {
            throw new InvalidOperationException(
                "Gateway:RequestLimits:DefaultMaxRequestBodySizeBytes must be between 1 and MaxRequestBodySizeBytes.");
        }

        foreach (var rule in Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Name)
                || (rule.Paths.Length == 0 && rule.PathPrefixes.Length == 0)
                || rule.Paths.Concat(rule.PathPrefixes).Any(path => string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
                || rule.MaxRequestBodySizeBytes < 1
                || rule.MaxRequestBodySizeBytes > MaxRequestBodySizeBytes)
            {
                throw new InvalidOperationException(
                    $"Gateway:RequestLimits rule '{rule.Name}' needs a name, '/'-rooted paths and a size between 1 and MaxRequestBodySizeBytes.");
            }
        }
    }

    /// <summary>The limit for <paramref name="path"/>: first matching rule, else the default.</summary>
    public long ResolveLimit(PathString path)
    {
        var value = path.Value is { Length: > 1 } raw ? raw.TrimEnd('/') : path.Value ?? "/";
        foreach (var rule in Rules)
        {
            if (rule.Paths.Any(candidate => string.Equals(candidate.TrimEnd('/'), value, StringComparison.OrdinalIgnoreCase))
                || rule.PathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return rule.MaxRequestBodySizeBytes;
            }
        }

        return DefaultMaxRequestBodySizeBytes;
    }
}

public sealed class GatewayRequestLimitRule
{
    public string Name { get; set; } = string.Empty;

    public string[] Paths { get; set; } = [];

    public string[] PathPrefixes { get; set; } = [];

    public long MaxRequestBodySizeBytes { get; set; }
}

/// <summary>
/// Request timeouts (section <c>Gateway:Timeouts</c>). The default applies to every route;
/// routes opt into <see cref="LongRunningPolicyName"/> in the route table or through
/// <see cref="LongRunningRouteIds"/>. Routes under <see cref="DisabledPathPrefixes"/>
/// (SignalR) are exempt.
/// </summary>
public sealed class GatewayTimeoutOptions
{
    public const string SectionName = "Gateway:Timeouts";
    public const string LongRunningPolicyName = "long-running";

    /// <summary>Equal to the previous YARP activity timeout, so ordinary routes keep their budget.</summary>
    public int DefaultSeconds { get; set; } = 100;

    public int LongRunningSeconds { get; set; } = 300;

    /// <summary>Existing route ids (protected route table) that get the long-running policy.</summary>
    public string[] LongRunningRouteIds { get; set; } = [];

    public string[] DisabledPathPrefixes { get; set; } = ["/hubs"];

    public void Validate()
    {
        if (DefaultSeconds is < 1 or > 3600 || LongRunningSeconds < DefaultSeconds || LongRunningSeconds > 3600)
        {
            throw new InvalidOperationException(
                "Gateway:Timeouts requires 1 <= DefaultSeconds <= LongRunningSeconds <= 3600.");
        }
    }
}

/// <summary>
/// Upstream defaults applied to every cluster that does not configure them itself
/// (section <c>Gateway:Proxy</c>), so the protected route table stays unchanged.
/// </summary>
public sealed class GatewayProxyOptions
{
    public const string SectionName = "Gateway:Proxy";

    /// <summary>Idle time allowed while waiting for an upstream; above the long-running timeout.</summary>
    public int ActivityTimeoutSeconds { get; set; } = 330;

    /// <summary>Recycle pooled connections so DNS round-robin reaches new replicas.</summary>
    public int PooledConnectionLifetimeSeconds { get; set; } = 120;

    public bool HealthChecksEnabled { get; set; } = true;

    public string ActiveHealthCheckPath { get; set; } = "/health/live";

    public int ActiveHealthCheckIntervalSeconds { get; set; } = 15;

    public int ActiveHealthCheckTimeoutSeconds { get; set; } = 5;

    public int PassiveReactivationSeconds { get; set; } = 30;

    public void Validate()
    {
        if (ActivityTimeoutSeconds < 1
            || PooledConnectionLifetimeSeconds < 1
            || ActiveHealthCheckIntervalSeconds < 1
            || ActiveHealthCheckTimeoutSeconds < 1
            || ActiveHealthCheckTimeoutSeconds >= ActiveHealthCheckIntervalSeconds
            || PassiveReactivationSeconds < 1
            || !ActiveHealthCheckPath.StartsWith('/'))
        {
            throw new InvalidOperationException(
                "Gateway:Proxy values must be positive, the active health check timeout shorter than its interval, and the path '/'-rooted.");
        }
    }
}
