using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.RegisterTeacher;

public record RegisterTeacherCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    PlatformProduct? Product = null
) : IRequest<Result<Guid>>;
