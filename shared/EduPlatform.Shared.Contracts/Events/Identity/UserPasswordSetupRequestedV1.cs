namespace EduPlatform.Shared.Contracts.Events.Identity;

/// <summary>
/// The password-setup e-mail for a newly created account. Only the Notification service consumes it,
/// so the one-time token no longer travels to every subscriber of <see cref="UserCreatedEvent"/>.
/// </summary>
public sealed record UserPasswordSetupRequestedV1(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string? Product,
    string PasswordSetupToken,
    DateTime PasswordSetupTokenExpiresAt,
    DateTime CreatedAt,
    string SchemaVersion = "1.0");
