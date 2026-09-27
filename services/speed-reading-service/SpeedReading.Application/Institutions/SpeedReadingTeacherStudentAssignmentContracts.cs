using SpeedReading.Domain.Institutions;

namespace SpeedReading.Application.Institutions;

public enum SpeedReadingTeacherStudentAssignmentResult
{
    Created,
    AlreadyActive,
    TeacherAssignmentConflict,
    MembershipRequired,
    ProductRoleRequired
}

public sealed record SpeedReadingTeacherStudentAssignmentRecord(
    Guid? InstitutionId,
    Guid TeacherUserId,
    Guid StudentUserId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record SpeedReadingTeacherStudentAssignmentPage(
    IReadOnlyList<SpeedReadingTeacherStudentAssignmentRecord> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record SpeedReadingTeacherStudentRosterRecord(
    Guid? InstitutionId,
    Guid TeacherUserId,
    Guid StudentUserId,
    DateTime AssignedAt,
    int? CurrentLevel,
    int? GradeLevel,
    int? TargetWpm,
    decimal? TargetComprehension,
    int? DailyGoalMinutes,
    string? LearningStyle,
    bool? IsProfileActive);

public sealed record SpeedReadingTeacherStudentRosterPage(
    IReadOnlyList<SpeedReadingTeacherStudentRosterRecord> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public interface ISpeedReadingTeacherStudentAssignments
{
    Task<SpeedReadingTeacherStudentAssignmentResult> AssignAsync(
        Guid? institutionId,
        Guid teacherUserId,
        Guid studentUserId,
        Guid actorId,
        DateTime at,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingTeacherStudentAssignmentPage> GetStudentsAsync(
        Guid institutionId,
        Guid teacherUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingTeacherStudentRosterPage> GetTeacherRosterAsync(
        Guid teacherUserId,
        IReadOnlyCollection<Guid> activeInstitutionIds,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        int? gradeLevel = null,
        Guid? studentUserId = null);

    Task<bool> RemoveAsync(
        Guid? institutionId,
        Guid teacherUserId,
        Guid studentUserId,
        Guid actorId,
        DateTime at,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetStudentUserIdsAsync(
        Guid teacherUserId,
        IReadOnlyCollection<Guid> requestedStudentUserIds,
        CancellationToken cancellationToken = default);
}
