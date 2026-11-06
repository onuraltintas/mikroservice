namespace EduPlatform.Shared.Contracts.Events.Identity;

public record UserForgotPasswordEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string ResetToken,
    string Role = "User")
{
    /// <summary>
    /// The product the reset e-mail is for ("coaching" or "speed-reading"): template, branding and link.
    /// Null (older publishers, accounts with both or neither) keeps the role-based choice.
    /// </summary>
    public string? Product { get; init; }
}
