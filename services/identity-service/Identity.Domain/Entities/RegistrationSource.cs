using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>Roadmap O11: the campaign tags a new account registered with, for one product.</summary>
public sealed class RegistrationSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public PlatformProduct Product { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    /// <summary>password or google.</summary>
    public string RegistrationMethod { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
