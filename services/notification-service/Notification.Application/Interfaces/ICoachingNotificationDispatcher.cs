namespace Notification.Application.Interfaces;

public interface ICoachingNotificationDispatcher
{
    Task SendAsync(
        Guid eventMessageId,
        IReadOnlyCollection<Guid> recipientIds,
        string title,
        string message,
        string type,
        string relatedEntityId,
        CancellationToken cancellationToken);
}
