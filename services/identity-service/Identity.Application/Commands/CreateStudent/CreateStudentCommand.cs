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
/// Response DTO containing StudentId and TemporaryPassword
/// </summary>
public record CreateStudentResult(Guid StudentId, string TemporaryPassword);
