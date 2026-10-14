namespace CourseManagement.Domain;

public sealed class StudentRecord
{
    private StudentRecord() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid? UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    public static StudentRecord Create(Guid id, Guid workspaceId, string displayName, string? email, string? phoneNumber, DateTimeOffset now)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty) throw new ArgumentException("Student and workspace identifiers are required.");
        return new StudentRecord
        {
            Id = id,
            WorkspaceId = workspaceId,
            DisplayName = Required(displayName, 200, nameof(displayName)),
            Email = Optional(email, 320, nameof(email))?.ToLowerInvariant(),
            PhoneNumber = Optional(phoneNumber, 40, nameof(phoneNumber)),
            CreatedAt = now
        };
    }

    public void LinkAccount(Guid userId)
    {
        if (userId == Guid.Empty || UserId is not null) throw new InvalidOperationException("Student account cannot be linked.");
        UserId = userId;
    }

    public void Update(string displayName, string? email, string? phoneNumber)
    {
        DisplayName = Required(displayName, 200, nameof(displayName));
        Email = Optional(email, 320, nameof(email))?.ToLowerInvariant();
        PhoneNumber = Optional(phoneNumber, 40, nameof(phoneNumber));
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void AnonymizeIdentity()
    {
        UserId = null;
        DisplayName = "Silinmiş öğrenci";
        Email = null;
        PhoneNumber = null;
    }

    private static string Required(string value, int maxLength, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Trim();
        if (normalized.Length is < 1 || normalized.Length > maxLength)
            throw new ArgumentException($"Value must contain between 1 and {maxLength} characters.", parameter);
        return normalized;
    }

    private static string? Optional(string? value, int maxLength, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Required(value, maxLength, parameter);
    }
}
