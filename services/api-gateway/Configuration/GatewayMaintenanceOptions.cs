namespace EduPlatform.Gateway.Configuration;

/// <summary>
/// Maintenance switches (section <c>Gateway:Maintenance</c>). Identity writes the flags
/// <c>system.maintenancemode</c> and <c>maintenance.{service}</c> to the shared cache;
/// the gateway enforces them.
/// </summary>
public sealed class GatewayMaintenanceOptions
{
    public const string SectionName = "Gateway:Maintenance";

    /// <summary>How long a flag read from Redis is reused; the delay before a switch takes effect.</summary>
    public int CacheSeconds { get; set; } = 5;

    /// <summary>Roles that keep working during maintenance (shipped in appsettings.json).</summary>
    public string[] BypassRoles { get; set; } = [];

    /// <summary>
    /// Path prefixes (segment-matched) that stay open so users can sign in and clients can
    /// read the public maintenance banner.
    /// </summary>
    public string[] BypassPathPrefixes { get; set; } = [];

    public void Validate()
    {
        if (CacheSeconds is < 0 or > 300)
        {
            throw new InvalidOperationException("Gateway:Maintenance:CacheSeconds must be between 0 and 300.");
        }

        if (BypassPathPrefixes.Any(prefix => string.IsNullOrWhiteSpace(prefix) || !prefix.StartsWith('/')))
        {
            throw new InvalidOperationException("Gateway:Maintenance:BypassPathPrefixes entries must start with '/'.");
        }
    }
}
