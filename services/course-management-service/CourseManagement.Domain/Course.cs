namespace CourseManagement.Domain;

public sealed class Course
{
    private Course() { }
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Course Create(Guid id, Guid workspaceId, string name, string subject, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty) throw new ArgumentException("Course and workspace identifiers are required.");
        return new Course { Id = id, WorkspaceId = workspaceId, Name = Text(name, 160, nameof(name)),
            Subject = Text(subject, 100, nameof(subject)), CreatedAt = now };
    }

    private static string Text(string value, int limit, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Trim();
        if (normalized.Length is < 1 || normalized.Length > limit) throw new ArgumentException("Course text is invalid.", parameter);
        return normalized;
    }
}
