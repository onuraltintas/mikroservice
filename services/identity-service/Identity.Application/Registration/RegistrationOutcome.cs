namespace Identity.Application.Registration;

/// <summary>
/// Result of a product registration (new account or an existing account joining the product).
/// <see cref="VerificationEmail"/> tells the client which success screen to show:
/// <see cref="Sent"/> when a verification e-mail went out (new account, or an existing account whose
/// address is not verified yet), <see cref="NotRequired"/> when the address is already verified and
/// the person can sign in now. Serialized as <c>{ userId, verificationEmail, newProductAccess }</c>; older clients read only userId.
/// </summary>
/// <param name="NewProductAccess">
/// Third review N3: true when this request granted the product (a new account or a first join), false for a
/// retry of a registration that had already happened. Clients count a sign-up only when it is true.
/// </param>
public sealed record RegistrationOutcome(Guid UserId, string VerificationEmail, bool NewProductAccess = true)
{
    public const string Sent = "sent";
    public const string NotRequired = "notRequired";

    public static RegistrationOutcome EmailSent(Guid userId, bool newProductAccess = true) => new(userId, Sent, newProductAccess);
    public static RegistrationOutcome AlreadyVerified(Guid userId, bool newProductAccess = true) => new(userId, NotRequired, newProductAccess);
}
