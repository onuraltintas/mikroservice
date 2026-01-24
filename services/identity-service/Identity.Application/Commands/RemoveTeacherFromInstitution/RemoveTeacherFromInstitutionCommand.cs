using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RemoveTeacherFromInstitution;

public record RemoveTeacherFromInstitutionCommand(Guid TeacherId) : IRequest<Result>;
