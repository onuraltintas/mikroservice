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

    /// <summary>Queues an e-mail that carries another sender's name, reply address and unsubscribe link.</summary>
    Task QueueFromAsync(Guid messageId, string consumerType, string recipient, string subject, string body,
        Notification.Domain.Entities.EmailSender sender, CancellationToken cancellationToken = default) =>
        QueueAsync(messageId, consumerType, recipient, subject, body, null, cancellationToken);
}
