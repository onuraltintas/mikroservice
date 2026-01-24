using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.LeaveTeacher;

public record LeaveTeacherCommand(Guid TeacherId) : IRequest<Result>;
