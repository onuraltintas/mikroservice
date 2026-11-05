using EduPlatform.Shared.Kernel.Results;
using Identity.Application.LegalPages;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.RegisterParent;

public record RegisterParentCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    PlatformProduct? Product = null,
    IReadOnlyList<LegalPageAcceptance>? LegalAcceptances = null,
    /// <summary>Roadmap O11: campaign tags of the landing page, stored with a new account.</summary>
    Identity.Application.Registration.RegistrationUtm? Utm = null,
    /// <summary>Second review M2b: the authenticator code of an existing MFA-protected account joining as parent.</summary>
    string? MfaCode = null
) : IRequest<Result<Identity.Application.Registration.RegistrationOutcome>>;
