namespace CourseManagement.Domain;

/// <summary>How the teacher found a student's homework in class (no student submission needed).</summary>
public enum CourseAssignmentCompletion
{
    NotMarked = 0,
    Done = 1,
    Partial = 2,
    NotDone = 3
}

public sealed class CourseAssignmentRecipient
{
    public const int MaxCompletionNoteLength = 500;

    private CourseAssignmentRecipient() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid StudentId { get; private set; }
    public CourseAssignmentCompletion Completion { get; private set; }
    public string? CompletionNote { get; private set; }
    public Guid? CompletionMarkedByUserId { get; private set; }
    public DateTimeOffset? CompletionMarkedAt { get; private set; }

    public static CourseAssignmentRecipient Create(Guid id, Guid workspaceId, Guid assignmentId, Guid studentId)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || assignmentId == Guid.Empty || studentId == Guid.Empty)
            throw new ArgumentException("Assignment recipient identifiers are required.");
        return new CourseAssignmentRecipient { Id = id, WorkspaceId = workspaceId, AssignmentId = assignmentId, StudentId = studentId };
    }

    /// <summary>Records the teacher's mark. Returns false when nothing changed (same mark and note).</summary>
    public bool MarkCompletion(CourseAssignmentCompletion completion, string? note, Guid markedByUserId, DateTimeOffset markedAt)
    {
        if (!Enum.IsDefined(completion)) throw new ArgumentOutOfRangeException(nameof(completion));
        if (markedByUserId == Guid.Empty || markedAt == default)
            throw new ArgumentException("The marking teacher and time are required.");
        var normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalized?.Length > MaxCompletionNoteLength)
            throw new ArgumentException($"A note cannot exceed {MaxCompletionNoteLength} characters.", nameof(note));
        if (completion == CourseAssignmentCompletion.NotMarked) normalized = null;
        if (Completion == completion && CompletionNote == normalized) return false;

        Completion = completion;
        CompletionNote = normalized;
        if (completion == CourseAssignmentCompletion.NotMarked)
        {
            CompletionMarkedByUserId = null;
            CompletionMarkedAt = null;
        }
        else
        {
            CompletionMarkedByUserId = markedByUserId;
            CompletionMarkedAt = markedAt.ToUniversalTime();
        }
        return true;
    }
}
