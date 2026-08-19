namespace Notification.Application.Interfaces;

public interface IEmailDeliveryQueue
{
    Task QueueAsync(
        Guid messageId,
        string consumerType,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}
