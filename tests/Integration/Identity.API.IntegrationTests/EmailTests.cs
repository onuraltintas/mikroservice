using FluentAssertions;
using Shared.IntegrationTests.Fixtures;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Integration tests for Email functionality using MailCatcher
/// </summary>
[Collection("Email")]
public class EmailTests
{
    private readonly MailCatcherFixture _mailCatcherFixture;
    private readonly ITestOutputHelper _output;

    public EmailTests(MailCatcherFixture mailCatcherFixture, ITestOutputHelper output)
    {
        _mailCatcherFixture = mailCatcherFixture;
        _output = output;
    }

    [Fact]
    public async Task SendEmail_ShouldAppearInMailCatcher()
    {
        // Arrange
        await _mailCatcherFixture.ClearMessagesAsync();

        using var smtpClient = new SmtpClient(_mailCatcherFixture.SmtpHost, _mailCatcherFixture.SmtpPort)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress("noreply@eduplatform.com"),
            Subject = "Test Email",
            Body = "This is a test email from integration tests",
            IsBodyHtml = false
        };
        mailMessage.To.Add("test@example.com");

        // Act
        await smtpClient.SendMailAsync(mailMessage);

        // Wait for email to be processed
        await Task.Delay(2000);

        // Assert
        var messagesJson = await _mailCatcherFixture.GetMessagesAsync();
        _output.WriteLine($"MailCatcher Response: {messagesJson}");

        // Parse as JsonDocument to handle dynamic structure
        using var doc = JsonDocument.Parse(messagesJson);
        var messages = doc.RootElement;

        messages.GetArrayLength().Should().BeGreaterThan(0, "at least one email should be received");
        
        var firstMessage = messages[0];
        var recipients = firstMessage.GetProperty("recipients").ToString();
        
        recipients.Should().Contain("test@example.com", "the email should be sent to test@example.com");
        firstMessage.GetProperty("subject").GetString().Should().Be("Test Email");
    }

    [Fact]
    public async Task SendMultipleEmails_ShouldAllAppearInMailCatcher()
    {
        // Arrange
        await _mailCatcherFixture.ClearMessagesAsync();

        using var smtpClient = new SmtpClient(_mailCatcherFixture.SmtpHost, _mailCatcherFixture.SmtpPort)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        // Act
        for (int i = 1; i <= 3; i++)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress("noreply@eduplatform.com"),
                Subject = $"Test Email {i}",
                Body = $"This is test email number {i}",
                IsBodyHtml = false
            };
            mailMessage.To.Add($"user{i}@example.com");

            await smtpClient.SendMailAsync(mailMessage);
        }

        // Wait for emails to be processed
        await Task.Delay(2000);

        // Assert
        var messagesJson = await _mailCatcherFixture.GetMessagesAsync();
        using var doc = JsonDocument.Parse(messagesJson);
        var messages = doc.RootElement;

        messages.GetArrayLength().Should().Be(3, "three emails should be received");
    }

    [Fact]
    public async Task ClearMessages_ShouldRemoveAllEmails()
    {
        // Arrange
        using var smtpClient = new SmtpClient(_mailCatcherFixture.SmtpHost, _mailCatcherFixture.SmtpPort)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress("noreply@eduplatform.com"),
            Subject = "Test Email",
            Body = "This email will be cleared",
            IsBodyHtml = false
        };
        mailMessage.To.Add("test@example.com");

        await smtpClient.SendMailAsync(mailMessage);
        await Task.Delay(1000);

        // Act
        await _mailCatcherFixture.ClearMessagesAsync();
        await Task.Delay(500);

        // Assert
        var messagesJson = await _mailCatcherFixture.GetMessagesAsync();
        using var doc = JsonDocument.Parse(messagesJson);
        var messages = doc.RootElement;

        messages.GetArrayLength().Should().Be(0, "all emails should be cleared");
    }
}
