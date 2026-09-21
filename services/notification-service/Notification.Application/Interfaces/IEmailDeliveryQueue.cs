namespace Notification.Application.Interfaces;

public interface IEmailDeliveryQueue
{
    Task QueueAsync(
        Guid messageId,
        string consumerType,
        string recipient,
        string subject,
        string body,
        Guid? subjectUserId = null,
        CancellationToken cancellationToken = default);
}
