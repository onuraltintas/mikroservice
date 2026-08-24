namespace EduPlatform.Shared.Contracts.Reporting;

/// <summary>
/// Teacher report scope resolved by Identity. Institution ids represent a
/// teacher allowed to view every student in that institution; explicit user
/// ids represent assignment-based access. This avoids sending large student
/// lists for institution-wide classes across service boundaries.
/// </summary>
public sealed record SpeedReadingTeacherStudentScopeRequest(
    Guid ViewerUserId,
    Guid? TargetTeacherUserId = null);

public sealed record SpeedReadingTeacherStudentScopeResponse(
    IReadOnlyList<Guid> InstitutionIds,
    IReadOnlyList<Guid> StudentUserIds,
    int TotalStudents);
