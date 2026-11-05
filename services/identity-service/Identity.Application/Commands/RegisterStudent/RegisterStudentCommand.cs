using EduPlatform.Shared.Kernel.Results;
using Identity.Application.LegalPages;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.RegisterStudent;

public record RegisterStudentCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    PlatformProduct? Product = null,
    IReadOnlyList<LegalPageAcceptance>? LegalAcceptances = null,
    /// <summary>Optional (Paket A, 05.11.2026): no longer asked at registration; an older client may still
    /// send it, and it is then kept on the Coaching student profile.</summary>
    DateOnly? BirthDate = null,
    /// <summary>Roadmap O11: campaign tags of the landing page, stored with a new account.</summary>
    Identity.Application.Registration.RegistrationUtm? Utm = null
) : IRequest<Result<Identity.Application.Registration.RegistrationOutcome>>;
