using SpeedReading.Domain.Institutions;

namespace SpeedReading.Application.Institutions;

public sealed record SpeedReadingInstitutionMembershipRecord(
    Guid InstitutionId,
    Guid UserId,
    SpeedReadingInstitutionMemberRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int? CurrentLevel,
    int? GradeLevel,
    int? TargetWpm,
    decimal? TargetComprehension,
    int? DailyGoalMinutes,
    string? LearningStyle,
    bool? IsProfileActive,
    Guid? TeacherUserId,
    int StudentCount);

public sealed record SpeedReadingInstitutionMemberView(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    SpeedReadingInstitutionMemberRole Role,
    bool IsMembershipActive,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int? CurrentLevel,
    int? GradeLevel,
    int? TargetWpm,
    decimal? TargetComprehension,
    int? DailyGoalMinutes,
    string? LearningStyle,
    Guid? TeacherUserId,
    string? TeacherName,
    int StudentCount);

public sealed record SpeedReadingInstitutionMembershipPage(
    IReadOnlyList<SpeedReadingInstitutionMembershipRecord> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public interface ISpeedReadingInstitutionMemberships
{
    Task<SpeedReadingInstitutionMembershipPage> GetMembersAsync(
        Guid institutionId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        SpeedReadingInstitutionMemberRole? role = null,
        bool? isActive = null,
        int? gradeLevel = null,
        Guid? teacherUserId = null,
        Guid? memberUserId = null);

    Task<bool> SetMembershipAsync(
        Guid institutionId,
        Guid userId,
        SpeedReadingInstitutionMemberRole role,
        bool isActive,
        Guid actorId,
        DateTime at,
        CancellationToken cancellationToken = default);
}

public enum SpeedReadingInstitutionStudentUpdateResult
{
    Updated,
    InvalidGradeLevel,
    TeacherAssignmentConflict,
    StudentMembershipRequired,
    TeacherMembershipRequired,
    ProductRoleRequired
}

public interface ISpeedReadingInstitutionStudentManagement
{
    Task<SpeedReadingInstitutionStudentUpdateResult> UpdateAsync(
        Guid institutionId,
        Guid studentUserId,
        int? gradeLevel,
        Guid? teacherUserId,
        Guid actorId,
        DateTime at,
        CancellationToken cancellationToken = default);
}

public interface ISpeedReadingInstitutionMemberEligibility
{
    Task<bool> IsEligibleAsync(
        Guid userId,
        SpeedReadingInstitutionMemberRole role,
        CancellationToken cancellationToken = default);
}

public interface ISpeedReadingInstitutionAdministrationAuthorization
{
    Task<bool> CanManageAsync(
        Guid userId,
        Guid institutionId,
        CancellationToken cancellationToken = default);
}
