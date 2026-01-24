namespace Notification.Application.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(Guid userId, string title, string message, string type, string? relatedEntityId = null);
}
