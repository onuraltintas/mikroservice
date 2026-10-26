namespace CourseManagement.Domain;

public sealed class CourseAssignmentAttachmentCleanupRequest
{
    private CourseAssignmentAttachmentCleanupRequest() { }

    public Guid Id { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public DateTimeOffset QueuedAt { get; private set; }

    public static CourseAssignmentAttachmentCleanupRequest Create(string storageKey, DateTimeOffset queuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        return new CourseAssignmentAttachmentCleanupRequest
        {
            Id = Guid.NewGuid(),
            StorageKey = storageKey,
            QueuedAt = queuedAt
        };
    }
}
