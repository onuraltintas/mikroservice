namespace CourseManagement.Domain;

public sealed class GuardianContact
{
    private GuardianContact() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static GuardianContact Create(Guid id, Guid workspaceId, string displayName, string? email, string? phoneNumber, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty) throw new ArgumentException("Guardian and workspace identifiers are required.");
        return new GuardianContact
        {
            Id = id,
            WorkspaceId = workspaceId,
            DisplayName = Required(displayName, 200, nameof(displayName)),
            Email = Optional(email, 320, nameof(email))?.ToLowerInvariant(),
            PhoneNumber = Optional(phoneNumber, 40, nameof(phoneNumber)),
            CreatedAt = now
        };
    }

    public void Update(string displayName, string? email, string? phoneNumber)
    {
        DisplayName = Required(displayName, 200, nameof(displayName));
        Email = Optional(email, 320, nameof(email))?.ToLowerInvariant();
        PhoneNumber = Optional(phoneNumber, 40, nameof(phoneNumber));
    }

    private static string Required(string value, int maxLength, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Trim();
        if (normalized.Length is < 1 || normalized.Length > maxLength)
            throw new ArgumentException($"Value must contain between 1 and {maxLength} characters.", parameter);
        return normalized;
    }

    private static string? Optional(string? value, int maxLength, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maxLength, parameter);
}
