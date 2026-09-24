namespace Coaching.Application.Interfaces;

/// <summary>
/// Supplies the active, tenant-scoped student roster for administrative reports.
/// Identity remains the source of truth for institution and grade membership.
/// </summary>
public interface ICoachingIdentityReportClient
{
    Task<IReadOnlyCollection<Guid>> GetActiveStudentIdsAsync(
        Guid viewerUserId,
        Guid institutionId,
        int? gradeLevel,
        CancellationToken cancellationToken);

    Task<CoachingStudentReportPage> GetActiveStudentPageAsync(
        Guid viewerUserId,
        Guid institutionId,
        int? gradeLevel,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken,
        string? search = null,
        Guid? teacherUserId = null);

    Task<CoachingTeacherReportPage> GetActiveTeacherPageAsync(
        Guid viewerUserId,
        Guid institutionId,
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);
}

public sealed record CoachingTeacherReportPage(
    IReadOnlyCollection<CoachingTeacherReportItem> Teachers,
    int TotalCount);

public sealed record CoachingTeacherReportItem(
    Guid UserId, string FirstName, string LastName, string Email);

public sealed record CoachingStudentReportPage(
    IReadOnlyCollection<Guid> StudentUserIds,
    int TotalCount,
    IReadOnlyCollection<CoachingStudentReportItem>? Students = null);

public sealed record CoachingStudentReportItem(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    int? GradeLevel,
    string? TeacherName,
    Guid? TeacherUserId = null);
