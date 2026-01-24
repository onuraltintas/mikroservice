using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;

    public UserCreatedConsumer(IEmailService emailService, INotificationService notificationService)
    {
        _emailService = emailService;
        _notificationService = notificationService;
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var message = context.Message;
        
        var subject = $"Welcome to EduPlatform, {message.FirstName}!";
        var body = $@"
            <html>
            <body>
                <h1>Welcome to EduPlatform!</h1>
                <p>Hello {message.FirstName} {message.LastName},</p>
                <p>Your account has been created successfully as a <strong>{message.Role}</strong>.</p>
                
                <div style='background-color: #f4f4f4; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <p>Since your account was created by an administrator, here is your temporary password:</p>
                    <h2 style='color: #2c3e50;'>{message.TemporaryPassword}</h2>
                </div>

                <p>Please login and change your password immediately.</p>
                
                <p>
                    <a href='http://localhost:3000/login' style='background-color: #3498db; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>
                        Login to EduPlatform
                    </a>
                </p>
            </body>
            </html>
        ";

        // In Parallel
        var emailTask = _emailService.SendEmailAsync(message.Email, subject, body);
        var notificationTask = _notificationService.SendNotificationAsync(
            message.UserId, 
            "Welcome to EduPlatform!", 
            "Your account has been created successfully.", 
            "Account"
        );

        await Task.WhenAll(emailTask, notificationTask);
    }
}
