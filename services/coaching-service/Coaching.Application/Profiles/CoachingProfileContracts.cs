using Coaching.Application.Interfaces;
using Coaching.Domain.Profiles;
using EduPlatform.Shared.Contracts.Coaching;

namespace Coaching.Application.Profiles;

public interface ICoachingAccountDirectory
{
    Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsActiveInstitutionAsync(Guid institutionId, CancellationToken cancellationToken);
    Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewerUserId, Guid studentUserId, CancellationToken cancellationToken);
}

public sealed record UpdateCoachingStudentProfile(int ExpectedVersion, CoachingEducationStatus EducationStatus,
    int? GradeLevel, string? SchoolName, string? SchoolCity, string? SchoolDistrict, string? SchoolType,
    string? ClassBranch, int? GraduationYear, DateOnly? BirthDate, string? Bio, string? AvatarUrl,
    bool ShareProgressWithTeachers);

public sealed record UpdateCoachingTeacherProfile(int ExpectedVersion, string? Title,
    IReadOnlyList<string> Subjects, int? ExperienceYears, string? Bio, string? AvatarUrl);

public sealed record TeacherStudentProfileUpdate([property:System.Text.Json.Serialization.JsonRequired] [System.ComponentModel.DataAnnotations.Range(0,int.MaxValue)] int ExpectedVersion,
    string? SchoolName,string? SchoolCity,string? SchoolDistrict,string? SchoolType,string? ClassBranch,DateOnly? BirthDate,string? Bio);

public sealed record CoachingOwnProfile(CoachingStudentProfile? Student, CoachingTeacherProfile? Teacher,
    IReadOnlyList<CoachingInstitutionMembership> Memberships);

public sealed record CoachingRosterItem(Guid UserId, string FirstName, string LastName, string FullName,
    string Email, int? GradeLevel, Guid? InstitutionId, string? InstitutionName, string? AvatarUrl,
    string? Subject, DateTime AssignmentStartDate, bool IsActive, DateTime? LastLoginAt,
    Guid? TeacherUserId = null, string? TeacherName = null);

public sealed record CoachingRosterPage(IReadOnlyList<CoachingRosterItem> Items, int TotalCount,
    int PageNumber, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
