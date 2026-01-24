using EduPlatform.Shared.Kernel.Primitives;
using Coaching.Domain.Enums;

namespace Coaching.Domain.Entities;

/// <summary>
/// Ödev (Assignment) - Aggregate Root
/// </summary>
public class Assignment : AggregateRoot
{
    public Guid TeacherId { get; private set; }
    public Guid? InstitutionId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Subject { get; private set; } // Matematik, Türkçe, Fen, etc.

    public AssignmentType Type { get; private set; }
    public int? TargetGradeLevel { get; private set; } // 1-12

    public DateTime DueDate { get; private set; }
    public int? EstimatedDurationMinutes { get; private set; }

    public decimal? MaxScore { get; private set; }
    public decimal? PassingScore { get; private set; }

    public AssignmentStatus Status { get; private set; }

    // Navigation
    private readonly List<AssignmentStudent> _assignedStudents = new();
    public IReadOnlyCollection<AssignmentStudent> AssignedStudents => _assignedStudents.AsReadOnly();

    private Assignment() { } // EF Core

    public static Assignment Create(
        Guid teacherId,
        string title,
        DateTime dueDate,
        AssignmentType type = AssignmentType.Individual,
        Guid? institutionId = null)
    {
        var assignment = new Assignment
        {
            TeacherId = teacherId,
            Title = title ?? throw new ArgumentNullException(nameof(title)),
            DueDate = dueDate,
            Type = type,
            InstitutionId = institutionId,
            Status = AssignmentStatus.Active
        };

        return assignment;
    }

    public void UpdateDetails(
        string? title = null,
        string? description = null,
        string? subject = null,
        DateTime? dueDate = null,
        int? estimatedDurationMinutes = null)
    {
        if (title != null) Title = title;
        if (description != null) Description = description;
        if (subject != null) Subject = subject;
        if (dueDate.HasValue) DueDate = dueDate.Value;
        if (estimatedDurationMinutes.HasValue) EstimatedDurationMinutes = estimatedDurationMinutes;

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetScoring(decimal maxScore, decimal? passingScore = null)
    {
        if (maxScore <= 0)
            throw new ArgumentException("Max score must be greater than 0", nameof(maxScore));

        MaxScore = maxScore;
        PassingScore = passingScore;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTargetGradeLevel(int gradeLevel)
    {
        if (gradeLevel < 1 || gradeLevel > 12)
            throw new ArgumentOutOfRangeException(nameof(gradeLevel), "Grade level must be between 1 and 12");

        TargetGradeLevel = gradeLevel;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToStudent(Guid studentId)
    {
        if (_assignedStudents.Any(s => s.StudentId == studentId))
            return; // Already assigned

        var assignmentStudent = AssignmentStudent.Create(Id, studentId);
        _assignedStudents.Add(assignmentStudent);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToStudents(IEnumerable<Guid> studentIds)
    {
        foreach (var studentId in studentIds)
        {
            AssignToStudent(studentId);
        }
    }

    public void Complete()
    {
        Status = AssignmentStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = AssignmentStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SubmitAssignment(Guid studentId, string? studentNote = null)
    {
        var studentAssignment = _assignedStudents.FirstOrDefault(s => s.StudentId == studentId);
        if (studentAssignment == null)
            throw new InvalidOperationException($"Student {studentId} is not assigned to this assignment.");

        studentAssignment.Submit(studentNote);
        UpdatedAt = DateTime.UtcNow;
    }

    public void GradeAssignment(Guid studentId, decimal score, string? feedback = null)
    {
        var studentAssignment = _assignedStudents.FirstOrDefault(s => s.StudentId == studentId);
        if (studentAssignment == null)
            throw new InvalidOperationException($"Student {studentId} is not assigned to this assignment.");

        if (MaxScore.HasValue && score > MaxScore.Value)
            throw new ArgumentException($"Score {score} exceeds maximum score {MaxScore.Value}");

        studentAssignment.Grade(score, feedback);
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Öğrenci-Ödev ilişkisi
/// </summary>
public class AssignmentStudent : Entity
{
    public Guid AssignmentId { get; private set; }
    public Assignment Assignment { get; private set; } = null!;

    public Guid StudentId { get; private set; }

    public DateTime? SubmittedAt { get; private set; }
    public decimal? Score { get; private set; }
    public string? TeacherFeedback { get; private set; }
    public string? StudentNote { get; private set; }

    public StudentAssignmentStatus Status { get; private set; }

    private AssignmentStudent() { }

    public static AssignmentStudent Create(Guid assignmentId, Guid studentId)
    {
        return new AssignmentStudent
        {
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = StudentAssignmentStatus.Assigned,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsInProgress()
    {
        if (Status == StudentAssignmentStatus.Assigned)
        {
            Status = StudentAssignmentStatus.InProgress;
        }
    }

    public void Submit(string? note = null)
    {
        SubmittedAt = DateTime.UtcNow;
        StudentNote = note;
        Status = StudentAssignmentStatus.Submitted;
    }

    public void Grade(decimal score, string? feedback = null)
    {
        Score = score;
        TeacherFeedback = feedback;
        Status = StudentAssignmentStatus.Graded;
    }
}
