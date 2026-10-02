namespace Notification.Infrastructure.Persistence;

public sealed class NotificationErasedRecipient
{
    public Guid UserId { get; private set; }
    public DateTime ErasedAt { get; private set; }
    private NotificationErasedRecipient() { }
    public static NotificationErasedRecipient Create(Guid userId, DateTime erasedAt)
    {
        if (userId == Guid.Empty || erasedAt.Kind != DateTimeKind.Utc) throw new ArgumentException("Valid recipient and UTC timestamp required.");
        return new() { UserId = userId, ErasedAt = erasedAt };
    }
}
