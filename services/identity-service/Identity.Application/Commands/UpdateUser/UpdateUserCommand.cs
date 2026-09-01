using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.UpdateUser;

public record UpdateUserCommand(
    Guid UserId, 
    string FirstName, 
    string LastName, 
    string? PhoneNumber,
    string? Bio = null,
    string? AvatarUrl = null,
    string? TeacherTitle = null,
    int? TeacherExperienceYears = null,
    string[]? TeacherSubjects = null,
    int? StudentGradeLevel = null,
    DateTime? StudentBirthDate = null,
    Identity.Domain.Enums.LearningStyle? StudentLearningStyle = null,
    Guid? InstitutionId = null,
    bool UpdateRoleProfile = false
) : IRequest<Result>;
