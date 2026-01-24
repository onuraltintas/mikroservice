namespace Notification.Domain.Entities;

public class NotificationItem
{
    public Guid Id { get;  set; }
    public Guid UserId { get;  set; }
    public string Title { get;  set; } = string.Empty;
    public string Message { get;  set; } = string.Empty;
    public string Type { get;  set; } = "Info"; // Invitation, System, etc.
    public bool IsRead { get;  set; }
    public DateTime CreatedAt { get;  set; }
    public string? RelatedEntityId { get; set; } // e.g. InvitationId

    public NotificationItem() { }

    public static NotificationItem Create(Guid userId, string title, string message, string type, string? relatedEntityId = null)
    {
        return new NotificationItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            RelatedEntityId = relatedEntityId
        };
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
