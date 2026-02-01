namespace EduPlatform.Shared.Contracts.Events.Identity;

public record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string VerificationToken
);
