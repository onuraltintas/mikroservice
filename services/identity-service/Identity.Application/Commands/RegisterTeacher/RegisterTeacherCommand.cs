using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RegisterTeacher;

public record RegisterTeacherCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone
) : IRequest<Result<Guid>>;
