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
    IReadOnlyList<LegalPageAcceptance>? LegalAcceptances = null
) : IRequest<Result<Guid>>;
