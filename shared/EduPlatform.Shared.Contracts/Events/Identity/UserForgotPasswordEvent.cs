namespace EduPlatform.Shared.Contracts.Events.Identity;

public record UserForgotPasswordEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string ResetToken);
