using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.UpdateUserProfile;

public record UpdateUserProfileCommand(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? Bio,
    string? AvatarUrl,
    // Teacher specific
    string? Title,
    string[]? Subjects,
    // Student specific
    int? GradeLevel,
    DateTime? BirthDate,
    Identity.Domain.Enums.LearningStyle? LearningStyle
) : IRequest<Result>;
