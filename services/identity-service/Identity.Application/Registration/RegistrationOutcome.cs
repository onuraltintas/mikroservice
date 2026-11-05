namespace Identity.Application.Registration;

/// <summary>
/// Result of a product registration (new account or an existing account joining the product).
/// <see cref="VerificationEmail"/> tells the client which success screen to show:
/// <see cref="Sent"/> when a verification e-mail went out (new account, or an existing account whose
/// address is not verified yet), <see cref="NotRequired"/> when the address is already verified and
/// the person can sign in now. Serialized as <c>{ userId, verificationEmail }</c>; older clients read only userId.
/// </summary>
public sealed record RegistrationOutcome(Guid UserId, string VerificationEmail)
{
    public const string Sent = "sent";
    public const string NotRequired = "notRequired";

    public static RegistrationOutcome EmailSent(Guid userId) => new(userId, Sent);
    public static RegistrationOutcome AlreadyVerified(Guid userId) => new(userId, NotRequired);
}
