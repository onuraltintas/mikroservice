using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.AssignStudent;

public record AssignStudentCommand(
    string StudentEmail,
    string? Message
) : IRequest<Result<Guid>>;
