namespace Identity.Application.Interfaces;

/// <summary>
/// Absolute limit of one sign-in. Refresh tokens rotate (each lasts
/// <c>JWT_REFRESH_TOKEN_EXPIRY_DAYS</c>), but the chain started by a sign-in ends after
/// <see cref="MaxSessionDays"/> and requires a new sign-in, including MFA.
/// Configured with <c>Identity:Session:MaxSessionDays</c>.
/// </summary>
public sealed class SessionLifetimeOptions
{
    public const string SectionName = "Identity:Session";

    public int MaxSessionDays { get; set; } = 30;

    public TimeSpan MaxSessionLifetime => TimeSpan.FromDays(MaxSessionDays);

    public void Validate()
    {
        if (MaxSessionDays is < 1 or > 365)
            throw new InvalidOperationException($"{SectionName}:MaxSessionDays must be between 1 and 365.");
    }
}
