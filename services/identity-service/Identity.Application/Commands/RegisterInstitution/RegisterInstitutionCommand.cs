using EduPlatform.Shared.Kernel.Results;
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
    string? City
) : IRequest<Result<Guid>>;
