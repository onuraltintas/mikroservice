namespace CourseManagement.Domain;

public sealed class CourseBranch
{
    private CourseBranch() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static CourseBranch Create(Guid id, Guid workspaceId, string name, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty)
            throw new ArgumentException("Branch identifiers are required.");
        ArgumentNullException.ThrowIfNull(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 120)
            throw new ArgumentException("Branch name must contain between 1 and 120 characters.", nameof(name));

        return new CourseBranch { Id = id, WorkspaceId = workspaceId, Name = normalizedName, CreatedAt = now.ToUniversalTime() };
    }
}
