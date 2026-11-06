namespace EduPlatform.Shared.Contracts.Events.Identity;

public record UserEmailConfirmedEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role
)
{
    /// <summary>
    /// The product the welcome e-mail is for ("coaching" or "speed-reading"): template, branding and login link.
    /// Null (older publishers, accounts with both or neither) keeps the role-based choice.
    /// </summary>
    public string? Product { get; init; }
}
