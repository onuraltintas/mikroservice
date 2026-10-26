using EduPlatform.Shared.Kernel.Results;
using Identity.Application.LegalPages;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.RegisterTeacher;

public record RegisterTeacherCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    PlatformProduct? Product = null,
    IReadOnlyList<LegalPageAcceptance>? LegalAcceptances = null
) : IRequest<Result<Guid>>;
