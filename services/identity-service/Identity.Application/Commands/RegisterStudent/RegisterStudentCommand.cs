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
    /// <summary>Roadmap Y8: asked at Coaching registration; kept on the Coaching student profile.</summary>
    DateOnly? BirthDate = null,
    /// <summary>Roadmap O11: campaign tags of the landing page, stored with a new account.</summary>
    Identity.Application.Registration.RegistrationUtm? Utm = null
) : IRequest<Result<Guid>>;
