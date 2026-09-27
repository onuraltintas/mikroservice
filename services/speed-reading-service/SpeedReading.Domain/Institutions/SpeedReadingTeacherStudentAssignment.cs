using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Institutions;

/// <summary>
/// A Speed Reading teacher's active relationship with a student, optionally
/// scoped to an institution. Identifiers are external references; this entity
/// deliberately has no cross-database foreign keys.
/// </summary>
public sealed class SpeedReadingTeacherStudentAssignment : AggregateRoot
{
    private SpeedReadingTeacherStudentAssignment()
    {
    }

    public Guid? InstitutionId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public bool IsActive { get; private set; }

    public static SpeedReadingTeacherStudentAssignment Create(
        Guid? institutionId,
        Guid teacherUserId,
        Guid studentUserId,
        Guid actorId,
        DateTime at)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution cannot be empty.", nameof(institutionId));
        if (teacherUserId == Guid.Empty)
            throw new ArgumentException("Teacher is required.", nameof(teacherUserId));
        if (studentUserId == Guid.Empty)
            throw new ArgumentException("Student is required.", nameof(studentUserId));
        if (teacherUserId == studentUserId)
            throw new ArgumentException("A teacher cannot be assigned to themselves.", nameof(studentUserId));
        if (actorId == Guid.Empty)
            throw new ArgumentException("Assignment actor is required.", nameof(actorId));

        return new SpeedReadingTeacherStudentAssignment
        {
            InstitutionId = institutionId,
            TeacherUserId = teacherUserId,
            StudentUserId = studentUserId,
            IsActive = true,
            CreatedAt = EnsureUtc(at),
            CreatedBy = actorId.ToString()
        };
    }

    public void SetActive(bool isActive, Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Assignment actor is required.", nameof(actorId));

        IsActive = isActive;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
