namespace EduPlatform.Shared.Contracts.Coaching;

/// <summary>One-time transfer contract. All relationship keys have already been resolved to account UserIds.</summary>
public sealed record CoachingLegacySnapshot(IReadOnlyList<CoachingLegacyStudent> Students,
    IReadOnlyList<CoachingLegacyTeacher> Teachers, IReadOnlyList<CoachingLegacyAssignment> Assignments);
public sealed record CoachingLegacyStudent(Guid UserId, int? GradeLevel, string? SchoolName, string? SchoolCity,
    DateOnly? BirthDate, string? Bio, bool ShareProgressWithTeachers, bool IsActive, Guid? InstitutionId,
    string? AvatarUrl = null);
public sealed record CoachingLegacyTeacher(Guid UserId, string? Title, IReadOnlyList<string> Subjects,
    int? ExperienceYears, string? Bio, string? AvatarUrl, bool CanViewAllInstitutionStudents, bool IsActive, Guid? InstitutionId);
public sealed record CoachingLegacyAssignment(Guid TeacherUserId, Guid StudentUserId, Guid? InstitutionId,
    string? Subject, bool IsActive, DateTime? StartDate = null, DateTime? EndDate = null);
