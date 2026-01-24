using EduPlatform.Shared.Kernel.Primitives;
using Coaching.Domain.Enums;

namespace Coaching.Domain.Entities;

/// <summary>
/// Koçluk Seansı - Aggregate Root
/// </summary>
public class CoachingSession : AggregateRoot
{
    public Guid TeacherId { get; private set; }
    public Guid? InstitutionId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public SessionType SessionType { get; private set; }

    public DateTime ScheduledDate { get; private set; }
    public int DurationMinutes { get; private set; }

    public string? MeetingLink { get; private set; } // Zoom, Teams, Google Meet link
    public SessionStatus Status { get; private set; }

    public string? TeacherNotes { get; private set; }

    // Navigation
    private readonly List<SessionAttendance> _attendances = new();
    public IReadOnlyCollection<SessionAttendance> Attendances => _attendances.AsReadOnly();

    private CoachingSession() { }

    public static CoachingSession Create(
        Guid teacherId,
        string title,
        DateTime scheduledDate,
        SessionType sessionType,
        int durationMinutes = 60,
        Guid? institutionId = null)
    {
        var session = new CoachingSession
        {
            TeacherId = teacherId,
            Title = title ?? throw new ArgumentNullException(nameof(title)),
            ScheduledDate = scheduledDate,
            SessionType = sessionType,
            DurationMinutes = durationMinutes > 0 ? durationMinutes : throw new ArgumentException("Duration must be greater than 0"),
            Status = SessionStatus.Scheduled,
            InstitutionId = institutionId
        };

        return session;
    }

    public void UpdateDetails(
        string? title = null,
        string? description = null,
        DateTime? scheduledDate = null,
        int? durationMinutes = null)
    {
        if (title != null) Title = title;
        if (description != null) Description = description;
        if (scheduledDate.HasValue) ScheduledDate = scheduledDate.Value;
        if (durationMinutes.HasValue && durationMinutes > 0) DurationMinutes = durationMinutes.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetMeetingLink(string meetingLink)
    {
        MeetingLink = meetingLink;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddTeacherNotes(string notes)
    {
        TeacherNotes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddStudent(Guid studentId)
    {
        if (_attendances.Any(a => a.StudentId == studentId))
            return; // Already added

        var attendance = SessionAttendance.Create(Id, studentId);
        _attendances.Add(attendance);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddStudents(IEnumerable<Guid> studentIds)
    {
        foreach (var studentId in studentIds)
        {
            AddStudent(studentId);
        }
    }

    public void Complete()
    {
        Status = SessionStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = SessionStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordAttendance(Guid studentId, bool attended, string? notes = null)
    {
        var attendance = _attendances.FirstOrDefault(a => a.StudentId == studentId);
        if (attendance == null) 
            throw new InvalidOperationException($"Student {studentId} is not added to this session.");

        var status = attended ? AttendanceStatus.Present : AttendanceStatus.Absent;
        attendance.MarkAttendance(status);
        
        if (notes != null)
            attendance.AddTeacherNote(notes);
            
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Seans Katılımı
/// </summary>
public class SessionAttendance : Entity
{
    public Guid SessionId { get; private set; }
    public CoachingSession Session { get; private set; } = null!;

    public Guid StudentId { get; private set; }

    public AttendanceStatus AttendanceStatus { get; private set; }
    public DateTime? JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }

    public string? StudentNote { get; private set; }
    public string? TeacherNote { get; private set; }

    private SessionAttendance() { }

    public static SessionAttendance Create(Guid sessionId, Guid studentId)
    {
        return new SessionAttendance
        {
            SessionId = sessionId,
            StudentId = studentId,
            AttendanceStatus = AttendanceStatus.Present, // Default
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAttendance(AttendanceStatus status, DateTime? joinedAt = null, DateTime? leftAt = null)
    {
        AttendanceStatus = status;
        JoinedAt = joinedAt;
        LeftAt = leftAt;
    }

    public void AddStudentNote(string note)
    {
        StudentNote = note;
    }

    public void AddTeacherNote(string note)
    {
        TeacherNote = note;
    }
}
