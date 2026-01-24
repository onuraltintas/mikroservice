using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RemoveStudentFromInstitution;

public record RemoveStudentFromInstitutionCommand(Guid StudentId) : IRequest<Result>;
