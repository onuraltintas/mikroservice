using EduPlatform.Shared.Kernel.Results;
using Identity.Application.LegalPages;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.RegisterInstitution;

public record RegisterInstitutionCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string InstitutionName,
    InstitutionType InstitutionType,
    string? Phone,
    string ProvinceId,
    string DistrictId,
    PlatformProduct? Product = null,
    IReadOnlyList<LegalPageAcceptance>? LegalAcceptances = null,
    /// <summary>Roadmap O11: campaign tags of the landing page, stored with a new account.</summary>
    Identity.Application.Registration.RegistrationUtm? Utm = null
) : IRequest<Result<Guid>>;
