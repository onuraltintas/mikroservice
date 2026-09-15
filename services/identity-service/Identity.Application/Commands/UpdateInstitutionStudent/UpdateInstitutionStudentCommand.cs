using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.UpdateInstitutionStudent;

public sealed record UpdateInstitutionStudentCommand(
    Guid StudentUserId,
    string FirstName,
    string LastName,
    int GradeLevel,
    Guid? TeacherUserId) : IRequest<Result>;
