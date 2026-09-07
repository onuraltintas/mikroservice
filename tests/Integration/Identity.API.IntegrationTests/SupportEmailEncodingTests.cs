using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Notification.Application.Commands.ReplyToSupportRequest;
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

    [Fact]
    public async Task Reply_EncodesOriginalAndAdminMessageAndRemovesSubjectLineBreaks()
    {
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var requestId = Guid.NewGuid();
        db.SupportRequests.Add(new SupportRequest(
            requestId,
            "<img src=x>",
            "<b>name</b>",
            "user@example.test",
            "Subject\r\nX-Injected: yes",
            "<script>alert(1)</script>"));
        await db.SaveChangesAsync();
        var queue = new CapturingQueue();

        var result = await new ReplyToSupportRequestHandler(db, queue).Handle(
            new ReplyToSupportRequestCommand(requestId, "<a href=x>reply</a>"),
            default);

        result.IsSuccess.Should().BeTrue();
        queue.Body.Should().Contain("&lt;script&gt;").And.Contain("&lt;a href").And.NotContain("<script").And.NotContain("<a href");
        queue.Subject.Should().NotContain("\r").And.NotContain("\n");
    }

    private sealed class CapturingQueue : IEmailDeliveryQueue
    {
        public string Body { get; private set; } = "";
        public string Subject { get; private set; } = "";
        public Task QueueAsync(Guid messageId, string consumerType, string recipient, string subject, string body, CancellationToken cancellationToken = default)
        { Subject = subject; Body = body; return Task.CompletedTask; }
    }
}
