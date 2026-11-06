namespace EduPlatform.Shared.Contracts.Events.Identity;

/// <summary>
/// E5 (#22): the account's e-mail address changed after the link sent to the new address was confirmed. The
/// notification service tells the previous address.
/// </summary>
public record UserEmailChangedEvent(
    Guid UserId,
    string PreviousEmail,
    string NewEmail,
    string FirstName,
    string LastName,
    string Role = "User")
{
    public string? Product { get; init; }
}
