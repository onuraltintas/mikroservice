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

    /// <summary>Fails start-up on an unusable SMTP port instead of failing every send.</summary>
    public static void ValidateConfiguration(IConfiguration configuration) => ResolvePort(configuration);

    private static int ResolvePort(IConfiguration configuration)
    {
        // Environment variables reach IConfiguration through the host's environment provider.
        var value = new[] { configuration["SMTP_PORT"], configuration["Email:Port"] }
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)) ?? "1025";
        return int.TryParse(value, out var port) && port is > 0 and <= 65535
            ? port
            : throw new InvalidOperationException($"SMTP port '{value}' is not a valid port number.");
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var email = new MimeMessage();
        
        // Check environment variables first for security
        var fromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") 
                        ?? _configuration["SMTP_FROM_EMAIL"] ?? _configuration["Email:From"] ?? "noreply@eduivme.com";
        var fromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") 
                      ?? _configuration["SMTP_FROM_NAME"] ?? _configuration["Email:FromName"] ?? "Eduİvme";
        email.From.Add(new MailboxAddress(fromName, fromEmail));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;
        email.Body = new TextPart(TextFormat.Html) { Text = body };

        using var smtp = new SmtpClient();
        
        var host = Environment.GetEnvironmentVariable("SMTP_HOST") 
                   ?? _configuration["SMTP_HOST"] ?? _configuration["Email:Host"] ?? "localhost";
        var port = ResolvePort(_configuration);
        var username = Environment.GetEnvironmentVariable("SMTP_USERNAME") 
                       ?? _configuration["SMTP_USERNAME"] ?? _configuration["Email:Username"];
        var password = Environment.GetEnvironmentVariable("SMTP_PASSWORD") 
                       ?? _configuration["SMTP_PASSWORD"] ?? _configuration["Email:Password"];

        // SSL/TLS için port 465, StartTLS için port 587
        SecureSocketOptions secureOption = port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.Auto
        };

        await smtp.ConnectAsync(host, port, secureOption, cancellationToken);
        
        // Authenticate if credentials provided
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            await smtp.AuthenticateAsync(username, password, cancellationToken);
        }
        
        await smtp.SendAsync(email, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }
}
