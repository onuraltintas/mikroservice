namespace CourseManagement.Domain;

public sealed class CourseAssignment
{
    private CourseAssignment() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid? GroupId { get; private set; }
    public Guid? StudentId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Instructions { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static CourseAssignment Create(Guid id, Guid workspaceId, Guid courseId, Guid? groupId, Guid? studentId,
        Guid teacherUserId, string title, string? instructions, DateTimeOffset dueAt, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || courseId == Guid.Empty || teacherUserId == Guid.Empty)
            throw new ArgumentException("Assignment identifiers are required.");
        if ((groupId is null) == (studentId is null) || groupId == Guid.Empty || studentId == Guid.Empty)
            throw new ArgumentException("An assignment must target exactly one group or student.");
        if (dueAt == default) throw new ArgumentException("Assignment due date is required.", nameof(dueAt));

        return new CourseAssignment
        {
            Id = id,
            WorkspaceId = workspaceId,
            CourseId = courseId,
            GroupId = groupId,
            StudentId = studentId,
            TeacherUserId = teacherUserId,
            Title = Required(title, 160, nameof(title)),
            Instructions = Optional(instructions, 4000, nameof(instructions)),
            DueAt = dueAt.ToUniversalTime(),
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    private static string Required(string value, int maximum, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Trim();
        if (normalized.Length is < 1 || normalized.Length > maximum)
            throw new ArgumentException($"Value must contain between 1 and {maximum} characters.", parameter);
        return normalized;
    }

    private static string? Optional(string? value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Required(value, maximum, parameter);
    }
}
