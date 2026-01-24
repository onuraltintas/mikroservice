using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class InvitationCreatedConsumer : IConsumer<InvitationCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;

    public InvitationCreatedConsumer(IEmailService emailService, INotificationService notificationService)
    {
        _emailService = emailService;
        _notificationService = notificationService;
    }

    public async Task Consume(ConsumeContext<InvitationCreatedEvent> context)
    {
        var message = context.Message;
        
        var subject = "Invitation to EduPlatform";
        var body = $@"
            <html>
            <body>
                <h1>You are invited!</h1>
                <p>Hello,</p>
                <p>You have been invited by <strong>{message.InviterEmail}</strong> to join EduPlatform.</p>
                
                {(string.IsNullOrEmpty(message.Message) ? "" : $"<p><em>Message: {message.Message}</em></p>")}
                
                <p>
                    <a href='{message.Link ?? "#"}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>
                        Accept Invitation
                    </a>
                </p>
                <p>Or copy this link: {message.Link}</p>
            </body>
            </html>
        ";

        var tasks = new List<Task>();
        tasks.Add(_emailService.SendEmailAsync(message.InviteeEmail, subject, body));

        if (message.InviteeId.HasValue)
        {
            tasks.Add(_notificationService.SendNotificationAsync(
                message.InviteeId.Value, 
                "You have a new invitation", 
                $"Invited by {message.InviterEmail}", 
                "Invitation", 
                message.InvitationId.ToString()
            ));
        }

        await Task.WhenAll(tasks);
    }
}
