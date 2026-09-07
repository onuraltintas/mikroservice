using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Notification.Application.Commands.SubmitSupportRequest;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

public sealed class SupportEmailEncodingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acknowledgement_EncodesUntrustedHtml(bool useTemplate)
    {
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        if (useTemplate)
        {
            db.EmailTemplates.Add(EmailTemplate.Create("Auth_SupportReceived", "Auth", "Received", "<p>{{FirstName}} {{LastName}} {{Subject}}</p>"));
            await db.SaveChangesAsync();
        }
        var queue = new CapturingQueue();
        var handler = new SubmitSupportRequestHandler(db, queue);
        var result = await handler.Handle(new SubmitSupportRequestCommand(
            "<img src=x>", "<b>name</b>", "user@example.test", "<a href=x>link</a>", "Test support message", "test-idempotency-key"), default);
        result.IsSuccess.Should().BeTrue();
        queue.Body.Should().Contain("&lt;img").And.Contain("&lt;a").And.NotContain("<img").And.NotContain("<a href");
    }
    private sealed class CapturingQueue : IEmailDeliveryQueue
    {
        public string Body { get; private set; } = "";
        public Task QueueAsync(Guid messageId, string consumerType, string recipient, string subject, string body, CancellationToken cancellationToken = default)
        { Body = body; return Task.CompletedTask; }
    }
}
