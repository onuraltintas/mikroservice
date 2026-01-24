using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Text;
using Notification.Application.Interfaces;

namespace Notification.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_configuration["Email:From"] ?? "no-reply@eduplatform.com"));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;
        email.Body = new TextPart(TextFormat.Html) { Text = body };

        using var smtp = new SmtpClient();
        
        var host = _configuration["Email:Host"] ?? "localhost";
        var port = int.Parse(_configuration["Email:Port"] ?? "1025");
        
        // MailPit (Dev) typically doesn't use SSL/TLS on port 1025
        await smtp.ConnectAsync(host, port, SecureSocketOptions.None, cancellationToken);
        
        // Auth logic can be added here if needed
        
        await smtp.SendAsync(email, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }
}
