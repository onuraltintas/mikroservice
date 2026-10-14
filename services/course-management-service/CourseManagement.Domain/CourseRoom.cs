namespace CourseManagement.Domain;

public sealed class CourseRoom
{
    private CourseRoom() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int? Capacity { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static CourseRoom Create(Guid id, Guid workspaceId, Guid branchId, string name, int? capacity, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Room identifiers are required.");
        ArgumentNullException.ThrowIfNull(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 120)
            throw new ArgumentException("Room name must contain between 1 and 120 characters.", nameof(name));
        if (capacity is < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        return new CourseRoom
        {
            Id = id, WorkspaceId = workspaceId, BranchId = branchId, Name = normalizedName,
            Capacity = capacity, CreatedAt = now.ToUniversalTime()
        };
    }
}
