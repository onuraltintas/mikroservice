using Coaching.Application.Interfaces;
using Coaching.Domain.Profiles;
using EduPlatform.Shared.Contracts.Coaching;

namespace Coaching.Application.Profiles;

/// <summary>Roadmap O12: one of the student's active teacher links. Only independent links can be ended by the student.</summary>
public sealed record CoachingStudentTeacherLink(Guid TeacherUserId, string TeacherName, Guid? InstitutionId, DateTime Since, bool CanLeave);

public interface ICoachingAccountDirectory
{
    Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsActiveInstitutionAsync(Guid institutionId, CancellationToken cancellationToken);
    Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewerUserId, Guid studentUserId, CancellationToken cancellationToken);

    /// <summary>
    /// V3 (parent panel): may this parent read the child's Coaching data? Identity answers from the verified link, the
    /// child's Coaching access and the child's "visible to my parent" switch; null means no. Agreements and payments keep
    /// <see cref="AuthorizeRepresentativeAsync"/>.
    /// </summary>
    Task<CoachingParentReadAuthorization?> AuthorizeParentReadAsync(Guid parentUserId, Guid studentUserId, CancellationToken cancellationToken)
        => throw new NotSupportedException("This account directory cannot authorize parent reads.");
}

public sealed record CoachingParentReadAuthorization(Guid RelationshipId, string Relationship);

public sealed record UpdateCoachingStudentProfile(int ExpectedVersion, CoachingEducationStatus EducationStatus,
    int? GradeLevel, string? SchoolName, string? SchoolCity, string? SchoolDistrict, string? SchoolType,
    string? ClassBranch, int? GraduationYear, DateOnly? BirthDate, string? Bio, string? AvatarUrl,
    bool ShareProgressWithTeachers)
{
    /// <summary>Paket C (#5, KVKK m.11): the home city and district from the shared location directory; null keeps them.</summary>
    public string? HomeProvinceId { get; init; }
    public string? HomeDistrictId { get; init; }
    /// <summary>Paket C: a new YKS diploma grade (50–100); ClearDiplomaGrade removes it; neither keeps it.</summary>
    public decimal? DiplomaGrade { get; init; }
    public bool DiplomaGradeEstimated { get; init; }
    public bool ClearDiplomaGrade { get; init; }
    /// <summary>
    /// Paket D (#7): the student's own school from the school catalog, as in the wizard; it sets the school's name, city,
    /// district and type from the catalog. Null keeps the school fields as sent (an older typed school stays until then).
    /// </summary>
    public Guid? SchoolId { get; init; }
}

public sealed record UpdateCoachingTeacherProfile(int ExpectedVersion, string? Title,
    IReadOnlyList<string> Subjects, int? ExperienceYears, string? Bio, string? AvatarUrl);

public sealed record TeacherStudentProfileUpdate([property:System.Text.Json.Serialization.JsonRequired] [System.ComponentModel.DataAnnotations.Range(0,int.MaxValue)] int ExpectedVersion,
    string? SchoolName,string? SchoolCity,string? SchoolDistrict,string? SchoolType,string? ClassBranch,DateOnly? BirthDate,string? Bio);

public sealed record CoachingOwnProfile(CoachingStudentProfile? Student, CoachingTeacherProfile? Teacher,
    IReadOnlyList<CoachingInstitutionMembership> Memberships)
{
    /// <summary>Paket D review (L2): the student's exam asks the YKS diploma grade (YKS, or no exam chosen yet).</summary>
    public bool ExamAsksDiploma { get; init; } = true;
}

public sealed record CoachingRosterItem(Guid UserId, string FirstName, string LastName, string FullName,
    string Email, int? GradeLevel, Guid? InstitutionId, string? InstitutionName, string? AvatarUrl,
    string? Subject, DateTime AssignmentStartDate, bool IsActive, DateTime? LastLoginAt,
    Guid? TeacherUserId = null, string? TeacherName = null);

public sealed record CoachingRosterPage(IReadOnlyList<CoachingRosterItem> Items, int TotalCount,
    int PageNumber, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
