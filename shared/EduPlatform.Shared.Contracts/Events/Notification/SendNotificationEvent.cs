namespace EduPlatform.Shared.Contracts.Events.Notification;

public record SendNotificationEvent(
    Guid UserId,
    string Title,
    string Message,
    string Type,
    string? RelatedEntityId = null);
