using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.CreateTeacher;

public record CreateTeacherCommand(
    string Email,
    string FirstName,
    string LastName,
    string? Title,
    string[] Subjects
) : IRequest<Result<CreateTeacherResult>>;

/// <summary>
/// The cross-service TeacherId is the Identity user id. ProfileId is exposed
/// separately for Identity-owned teacher-profile and invitation operations.
/// </summary>
public record CreateTeacherResult(Guid TeacherId, Guid ProfileId);
