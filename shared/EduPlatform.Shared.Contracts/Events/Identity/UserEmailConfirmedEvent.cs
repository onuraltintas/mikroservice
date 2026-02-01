namespace EduPlatform.Shared.Contracts.Events.Identity;

public record UserEmailConfirmedEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role
);
