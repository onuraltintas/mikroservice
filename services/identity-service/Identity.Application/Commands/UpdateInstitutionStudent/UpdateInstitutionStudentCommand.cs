using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.UpdateInstitutionStudent;

public sealed record UpdateInstitutionStudentCommand(
    Guid StudentUserId,
    int GradeLevel,
    Guid? TeacherUserId) : IRequest<Result>;
