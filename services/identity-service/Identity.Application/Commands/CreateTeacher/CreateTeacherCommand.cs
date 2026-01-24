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
/// Response DTO containing CreatedTeacherId and TemporaryPassword
/// </summary>
public record CreateTeacherResult(Guid TeacherId, string TemporaryPassword);
