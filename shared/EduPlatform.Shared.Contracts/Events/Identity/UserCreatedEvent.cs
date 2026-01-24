namespace EduPlatform.Shared.Contracts.Events.Identity;

public record UserCreatedEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string TemporaryPassword,
    DateTime CreatedAt
);
