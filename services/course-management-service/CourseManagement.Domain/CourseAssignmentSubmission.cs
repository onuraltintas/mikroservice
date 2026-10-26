namespace CourseManagement.Domain;

public sealed class CourseAssignmentSubmission
{
    private CourseAssignmentSubmission() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid StudentId { get; private set; }
    public string? Text { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public decimal? Score { get; private set; }
    public string? Feedback { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public static CourseAssignmentSubmission Create(Guid id, Guid workspaceId, Guid assignmentId, Guid studentId,
        string? text, DateTimeOffset submittedAt)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || assignmentId == Guid.Empty || studentId == Guid.Empty)
            throw new ArgumentException("Assignment submission identifiers are required.");
        return new CourseAssignmentSubmission
        {
            Id = id,
            WorkspaceId = workspaceId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Text = Normalize(text),
            SubmittedAt = submittedAt.ToUniversalTime()
        };
    }

    public void UpdateSubmission(string? text, DateTimeOffset submittedAt)
    {
        Text = Normalize(text);
        SubmittedAt = submittedAt.ToUniversalTime();
        Score = null;
        Feedback = null;
        ReviewedByUserId = null;
        ReviewedAt = null;
    }

    public void Review(decimal? score, string? feedback, Guid reviewerUserId, DateTimeOffset reviewedAt)
    {
        if (score is < 0 or > 100 || score is { } value && decimal.Round(value, 2) != value)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100 with up to two decimal places.");
        if (score is null && string.IsNullOrWhiteSpace(feedback))
            throw new ArgumentException("A score or feedback is required.", nameof(feedback));
        if (reviewerUserId == Guid.Empty || reviewedAt == default)
            throw new ArgumentException("Review identifiers and time are required.");

        Score = score;
        Feedback = Normalize(feedback, 3000);
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = reviewedAt.ToUniversalTime();
    }

    private static string? Normalize(string? value, int maximum = 8000)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximum) throw new ArgumentException($"Text cannot exceed {maximum} characters.", nameof(value));
        return normalized;
    }
}
