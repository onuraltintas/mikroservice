using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RegisterStudent;

public record RegisterStudentCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone
) : IRequest<Result<Guid>>;
