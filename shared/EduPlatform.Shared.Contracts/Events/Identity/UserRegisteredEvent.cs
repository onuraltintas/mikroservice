namespace EduPlatform.Shared.Contracts.Events.Identity;

public record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string VerificationToken,
    string Role = "Student"
)
{
    /// <summary>
    /// The product the verification e-mail is for ("coaching" or "speed-reading"); it decides the
    /// template, branding and link origin. Null (older publishers, resend) keeps the role-based choice.
    /// </summary>
    public string? Product { get; init; }
}
