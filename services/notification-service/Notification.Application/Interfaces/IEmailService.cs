namespace Notification.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>Sends with another sender's name, reply address and unsubscribe link (all optional).</summary>
    Task SendEmailAsync(string to, string subject, string body, Notification.Domain.Entities.EmailSender? sender,
        CancellationToken cancellationToken = default) => SendEmailAsync(to, subject, body, cancellationToken);
}
