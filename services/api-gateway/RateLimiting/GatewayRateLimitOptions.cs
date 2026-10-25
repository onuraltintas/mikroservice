namespace EduPlatform.Gateway.RateLimiting;

/// <summary>
/// Gateway rate limits (section <c>Gateway:RateLimiting</c>). Route rules are evaluated in
/// order and the first match wins; rules sharing a <see cref="GatewayRateLimitRule.Name"/>
/// share one budget. Authenticated requests are additionally limited per user.
/// </summary>
public sealed class GatewayRateLimitOptions
{
    public const string SectionName = "Gateway:RateLimiting";

    /// <summary>Master switch, e.g. for an isolated load-test environment.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Redis key prefix for the shared counters.</summary>
    public string KeyPrefix { get; set; } = "EduPlatform:RateLimit";

    /// <summary>After a Redis failure, use only the local fallback for this many seconds.</summary>
    public int RedisRetryAfterFailureSeconds { get; set; } = 5;

    public List<GatewayRateLimitRule> Rules { get; set; } = [];

    public GatewayUserRateLimitOptions AuthenticatedUser { get; set; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(KeyPrefix))
        {
            throw new InvalidOperationException("Gateway:RateLimiting:KeyPrefix must not be empty.");
        }

        if (RedisRetryAfterFailureSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException("Gateway:RateLimiting:RedisRetryAfterFailureSeconds must be between 1 and 300.");
        }

        for (var index = 0; index < Rules.Count; index++)
        {
            Rules[index].Validate($"Gateway:RateLimiting:Rules:{index}");
        }

        AuthenticatedUser.Validate();
    }
}

public sealed class GatewayRateLimitRule
{
    public string Name { get; set; } = string.Empty;

    /// <summary>HTTP methods the rule applies to; empty means every method.</summary>
    public string[] Methods { get; set; } = [];

    /// <summary>Exact request paths (case-insensitive).</summary>
    public string[] Paths { get; set; } = [];

    /// <summary>Path prefixes matched on segment boundaries.</summary>
    public string[] PathPrefixes { get; set; } = [];

    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    public RateLimitPartitionKind Partition { get; set; } = RateLimitPartitionKind.Ip;

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);

    internal void Validate(string location)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException($"{location}: a rule name is required.");
        }

        if (PermitLimit < 1)
        {
            throw new InvalidOperationException($"{location} ({Name}): PermitLimit must be at least 1.");
        }

        if (WindowSeconds is < 1 or > 86_400)
        {
            throw new InvalidOperationException($"{location} ({Name}): WindowSeconds must be between 1 and 86400.");
        }

        if (Paths.Length == 0 && PathPrefixes.Length == 0)
        {
            throw new InvalidOperationException($"{location} ({Name}): at least one entry in Paths or PathPrefixes is required.");
        }

        foreach (var path in Paths.Concat(PathPrefixes))
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
            {
                throw new InvalidOperationException($"{location} ({Name}): paths must start with '/'.");
            }
        }
    }
}

public sealed class GatewayUserRateLimitOptions
{
    public const string RuleName = "authenticated-user";

    public bool Enabled { get; set; } = true;

    public int PermitLimit { get; set; } = 600;

    public int WindowSeconds { get; set; } = 60;

    /// <summary>Paths never counted against the user budget (long-lived SignalR connections).</summary>
    public string[] ExcludedPathPrefixes { get; set; } = ["/hubs"];

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);

    internal void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (PermitLimit < 1 || WindowSeconds is < 1 or > 86_400)
        {
            throw new InvalidOperationException(
                "Gateway:RateLimiting:AuthenticatedUser requires PermitLimit >= 1 and WindowSeconds between 1 and 86400.");
        }
    }
}

public enum RateLimitPartitionKind
{
    /// <summary>The client address resolved by the trusted-proxy configuration.</summary>
    Ip,

    /// <summary>The authenticated subject, falling back to the client address.</summary>
    User
}
