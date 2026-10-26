using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RemoveStudentFromTeacher;

public record RemoveStudentFromTeacherCommand(Guid StudentId) : IRequest<Result>;
