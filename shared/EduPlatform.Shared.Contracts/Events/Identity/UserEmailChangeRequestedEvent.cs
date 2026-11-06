namespace EduPlatform.Shared.Contracts.Events.Identity;

/// <summary>
/// E5 (#22): the account asked to change its e-mail address. The notification service sends the one-time link to
/// <see cref="NewEmail"/>; the address changes only when the link is confirmed.
/// </summary>
public record UserEmailChangeRequestedEvent(
    Guid UserId,
    string NewEmail,
    string FirstName,
    string LastName,
    string ConfirmationToken,
    string Role = "User")
{
    /// <summary>The site of the link and branding ("coaching"; a management role gets the admin panel).</summary>
    public string? Product { get; init; }
}
