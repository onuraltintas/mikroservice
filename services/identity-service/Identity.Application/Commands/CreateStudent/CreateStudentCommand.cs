using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.CreateStudent;

public record CreateStudentCommand(
    string Email,
    string FirstName,
    string LastName,
    string StudentNumber,
    int GradeLevel
) : IRequest<Result<CreateStudentResult>>;

/// <summary>
/// The cross-service StudentId is the Identity user id. ProfileId is exposed
/// separately for Identity-owned student-profile operations.
/// </summary>
public record CreateStudentResult(Guid StudentId, Guid ProfileId);
