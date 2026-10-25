using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

/// <summary>
/// Öğretmen-Öğrenci Ataması
/// </summary>
public class TeacherStudentAssignment : Entity
{
    public Guid TeacherId { get; private set; }
    public TeacherProfile Teacher { get; private set; } = null!;

    public Guid StudentId { get; private set; }
    public StudentProfile Student { get; private set; } = null!;

    public Guid? InstitutionId { get; private set; }
    public Institution? Institution { get; private set; }

    public string? Subject { get; private set; } // Hangi ders için atama
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Guid? CreatedByUserId { get; private set; }

    private TeacherStudentAssignment() { }

    public static TeacherStudentAssignment Create(
        Guid teacherId,
        Guid studentId,
        Guid? institutionId = null,
        string? subject = null,
        Guid? createdByUserId = null)
    {
        return new TeacherStudentAssignment
        {
            TeacherId = teacherId,
            StudentId = studentId,
            InstitutionId = institutionId,
            Subject = subject,
            StartDate = DateTime.UtcNow,
            CreatedByUserId = createdByUserId
        };
    }

    public void UpdateSubject(string subject)
    {
        Subject = subject;
        UpdatedAt = DateTime.UtcNow;
    }

    public void End()
    {
        EndDate = DateTime.UtcNow;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        EndDate = null;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
