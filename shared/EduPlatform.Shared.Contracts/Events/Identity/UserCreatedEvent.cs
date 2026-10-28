namespace EduPlatform.Shared.Contracts.Events.Identity;

/// <summary>
/// A new account. Every interested service receives it, so it carries no secrets.
/// </summary>
/// <param name="PasswordSetupToken">
/// Always null from current Identity versions; the token is sent only to Notification in
/// <see cref="UserPasswordSetupRequestedV1"/>. Kept so that messages published by older versions
/// (still in a queue during a deployment) are read and handled as before.
/// </param>
public record UserCreatedEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string? PasswordSetupToken,
    DateTime PasswordSetupTokenExpiresAt,
    DateTime CreatedAt
)
{
    public string? Product { get; init; }
}
