namespace CourseManagement.Domain;

public sealed class CourseGroup
{
    private CourseGroup() { }
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static CourseGroup Create(Guid id, Guid workspaceId, Guid courseId, Guid teacherUserId, string name, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || courseId == Guid.Empty || teacherUserId == Guid.Empty)
            throw new ArgumentException("Group identifiers and teacher are required.");
        ArgumentNullException.ThrowIfNull(name);
        var normalized = name.Trim();
        if (normalized.Length is < 1 or > 160) throw new ArgumentException("Group name is invalid.", nameof(name));
        return new CourseGroup { Id = id, WorkspaceId = workspaceId, CourseId = courseId,
            TeacherUserId = teacherUserId, Name = normalized, CreatedAt = now };
    }
}
