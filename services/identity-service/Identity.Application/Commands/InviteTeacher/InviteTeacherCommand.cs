using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.InviteTeacher;

public record InviteTeacherCommand(
    string TeacherEmail,
    string? Message
) : IRequest<Result<Guid>>;
