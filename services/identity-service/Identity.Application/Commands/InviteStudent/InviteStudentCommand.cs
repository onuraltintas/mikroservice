using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.InviteStudent;

public record InviteStudentCommand(
    string StudentEmail,
    string? Message = null,
    Guid? TeacherUserId = null
) : IRequest<Result<Guid>>;
