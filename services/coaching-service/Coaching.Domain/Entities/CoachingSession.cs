using EduPlatform.Shared.Kernel.Primitives;
using EduPlatform.Shared.Kernel.Exceptions;
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
    public CoachingNoteVisibility TeacherNotesVisibility { get; private set; } = CoachingNoteVisibility.CoachPrivate;

    /// <summary>Roadmap O6: the recurring series this session belongs to (for example every Tuesday 18:00).</summary>
    public Guid? SeriesId { get; private set; }

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
            ScheduledDate = CoachingTime.ToUtc(scheduledDate),
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
        if (scheduledDate.HasValue) ScheduledDate = CoachingTime.ToUtc(scheduledDate.Value);
        if (durationMinutes.HasValue && durationMinutes > 0) DurationMinutes = durationMinutes.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Replaces the editable session fields. Null optional values are cleared.
    /// </summary>
    public void UpdateEditableDetails(
        string title,
        string? description,
        DateTime scheduledDate,
        int durationMinutes,
        string? meetingLink,
        string? teacherNotes,
        CoachingNoteVisibility teacherNotesVisibility = CoachingNoteVisibility.CoachPrivate)
    {
        if (Status != SessionStatus.Scheduled)
            throw new BusinessRuleException(
                "Session.NotEditable",
                "Yalnızca planlanmış seanslar yeniden planlanabilir.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (durationMinutes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                "Duration must be greater than 0.");

        if (scheduledDate <= DateTime.UtcNow)
            throw new BusinessRuleException(
                "Session.NotEditable",
                "Seans başlangıcı gelecekte olmalıdır.");

        Title = title.Trim();
        Description = NormalizeOptional(description);
        ScheduledDate = CoachingTime.ToUtc(scheduledDate);
        DurationMinutes = durationMinutes;
        MeetingLink = NormalizeOptional(meetingLink);
        TeacherNotes = NormalizeOptional(teacherNotes);
        TeacherNotesVisibility = TeacherNotes is null
            ? CoachingNoteVisibility.CoachPrivate
            : Enum.IsDefined(teacherNotesVisibility)
                ? teacherNotesVisibility
                : throw new ArgumentOutOfRangeException(nameof(teacherNotesVisibility));
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignSeries(Guid seriesId)
    {
        if (seriesId == Guid.Empty) throw new ArgumentException("Series id is required.", nameof(seriesId));
        SeriesId = seriesId;
    }

    /// <summary>
    /// Roadmap O6: who takes part in a planned future session. Students without a recorded attendance can be removed;
    /// returns who was added and who was removed.
    /// </summary>
    public (Guid[] Added, Guid[] Removed) SetParticipants(IReadOnlyCollection<Guid> studentIds, DateTime now)
    {
        if (Status != SessionStatus.Scheduled || ScheduledDate <= now)
            throw new BusinessRuleException("Session.NotEditable", "Katılımcılar yalnız gelecekteki planlanmış seanslarda değiştirilebilir.");
        var wanted = studentIds.Where(id => id != Guid.Empty).Distinct().ToHashSet();
        var removed = _attendances.Where(attendance => !wanted.Contains(attendance.StudentId)).ToArray();
        if (removed.Any(attendance => attendance.AttendanceStatus != AttendanceStatus.NotRecorded))
            throw new BusinessRuleException("Session.NotEditable", "Katılımı girilmiş öğrenci seanstan çıkarılamaz.");
        foreach (var attendance in removed) _attendances.Remove(attendance);
        var added = wanted.Where(id => _attendances.All(attendance => attendance.StudentId != id)).ToArray();
        foreach (var studentId in added) _attendances.Add(SessionAttendance.Create(Id, studentId));
        if (added.Length > 0 || removed.Length > 0) UpdatedAt = DateTime.UtcNow;
        return (added, removed.Select(attendance => attendance.StudentId).ToArray());
    }

    public void SetMeetingLink(string meetingLink)
    {
        MeetingLink = meetingLink;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddTeacherNotes(
        string notes,
        CoachingNoteVisibility visibility = CoachingNoteVisibility.CoachPrivate)
    {
        if (string.IsNullOrWhiteSpace(notes))
            throw new ArgumentException("Notes are required.", nameof(notes));
        if (!Enum.IsDefined(visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));

        TeacherNotes = notes.Trim();
        TeacherNotesVisibility = visibility;
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
        if (Status != SessionStatus.Scheduled || ScheduledDate <= DateTime.UtcNow)
            throw new BusinessRuleException(
                "Session.NotCancellable",
                "Yalnızca gelecekteki planlanmış seanslar iptal edilebilir.");

        Status = SessionStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records one student's attendance once the session has started. A cancelled session has no attendance.
    /// When every student is recorded, whatever the status, the session is held and becomes Completed.
    /// </summary>
    public void RecordAttendance(Guid studentId, AttendanceStatus status, string? notes, DateTime now)
    {
        if (Status == SessionStatus.Cancelled)
            throw new BusinessRuleException("Session.AttendanceNotAllowed", "İptal edilmiş seansa katılım girilemez.");
        if (ScheduledDate > now)
            throw new BusinessRuleException("Session.AttendanceNotAllowed", "Seans başlamadan katılım girilemez.");
        if (status is not (AttendanceStatus.Present or AttendanceStatus.Absent or AttendanceStatus.Late or AttendanceStatus.Excused))
            throw new ArgumentOutOfRangeException(nameof(status));
        var attendance = _attendances.FirstOrDefault(a => a.StudentId == studentId)
            ?? throw new InvalidOperationException($"Student {studentId} is not added to this session.");

        attendance.MarkAttendance(status);
        if (notes != null)
            attendance.AddTeacherNote(notes);
        if (Status == SessionStatus.Scheduled && _attendances.All(a => a.AttendanceStatus != AttendanceStatus.NotRecorded))
            Status = SessionStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>A session that has been held keeps its record; only unheld sessions can be removed.</summary>
    public bool HasAttendanceHistory =>
        Status == SessionStatus.Completed || _attendances.Any(a => a.AttendanceStatus != AttendanceStatus.NotRecorded);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
            AttendanceStatus = AttendanceStatus.NotRecorded,
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
