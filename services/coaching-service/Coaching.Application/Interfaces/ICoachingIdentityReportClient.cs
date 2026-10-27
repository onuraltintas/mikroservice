namespace Coaching.Application.Interfaces;

/// <summary>
/// Supplies the active, tenant-scoped student roster for administrative reports.
/// Coaching owns profiles, grade membership and assignments; Identity supplies shared account facts.
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

    /// <summary>
    /// The institution's member roster for its administration: every active member, including students who do not
    /// share their progress (their rows say so). Progress reports keep using <see cref="GetActiveStudentPageAsync"/>.
    /// </summary>
    Task<CoachingStudentReportPage> GetMemberStudentPageAsync(
        Guid viewerUserId,
        Guid institutionId,
        int? gradeLevel,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken,
        string? search = null,
        Guid? teacherUserId = null) =>
        GetActiveStudentPageAsync(viewerUserId, institutionId, gradeLevel, pageNumber, pageSize, cancellationToken, search, teacherUserId);

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
    Guid? TeacherUserId = null,
    bool ProgressShared = true,
    IReadOnlyList<CoachingStudentTeacher>? Teachers = null);

public sealed record CoachingStudentTeacher(Guid UserId, string Name);
