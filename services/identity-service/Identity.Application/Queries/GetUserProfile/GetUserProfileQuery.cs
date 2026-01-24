using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Queries.GetUserProfile;

public class UserProfileDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    
    // Common fields
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }

    // Conditional fields based on role
    public TeacherDetailsDto? TeacherDetails { get; set; }
    public StudentDetailsDto? StudentDetails { get; set; }
}

public class TeacherDetailsDto
{
    public string? Title { get; set; }
    public string[] Subjects { get; set; } = Array.Empty<string>();
    public Guid? InstitutionId { get; set; }
    public string? InstitutionName { get; set; }
}

public class StudentDetailsDto
{
    public int? GradeLevel { get; set; }
    public string? StudentNumber { get; set; }
    public Guid? InstitutionId { get; set; }
    public string? InstitutionName { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? LearningStyle { get; set; }
}

public record GetUserProfileQuery(Guid UserId) : IRequest<Result<UserProfileDto>>;
