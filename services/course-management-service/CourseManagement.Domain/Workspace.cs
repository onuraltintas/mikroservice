namespace CourseManagement.Domain;

public enum WorkspaceType
{
    Institution = 1,
    IndependentTeacher = 2
}

public sealed class Workspace
{
    private Workspace() { }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public WorkspaceType Type { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Workspace Create(Guid id, Guid ownerUserId, string name, WorkspaceType type)
    {
        if (id == Guid.Empty || ownerUserId == Guid.Empty || !Enum.IsDefined(type))
            throw new ArgumentException("Valid workspace identifiers and type are required.");
        ArgumentNullException.ThrowIfNull(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 200)
            throw new ArgumentException("Workspace name must contain between 1 and 200 characters.", nameof(name));

        return new Workspace
        {
            Id = id,
            OwnerUserId = ownerUserId,
            Name = normalizedName,
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
