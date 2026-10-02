using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

public sealed class EmailDeliveryQueue : IEmailDeliveryQueue
{
    private readonly NotificationDbContext _dbContext;
    private readonly IDataProtector _bodyProtector;

    public EmailDeliveryQueue(NotificationDbContext dbContext, IDataProtectionProvider dataProtectionProvider)
    {
        _dbContext = dbContext;
        _bodyProtector = dataProtectionProvider.CreateProtector("EduPlatform.Notification.EmailDelivery.v1");
    }

    public async Task QueueAsync(
        Guid messageId,
        string consumerType,
        string recipient,
        string subject,
        string body,
        Guid? subjectUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (subjectUserId.HasValue)
        {
            await NotificationRecipientWrites.LockedAsync(_dbContext, subjectUserId.Value, async () =>
            {
                if (!await _dbContext.ErasedRecipients.AnyAsync(x => x.UserId == subjectUserId.Value, cancellationToken))
                    await InsertAsync(messageId, consumerType, recipient, subject, body, subjectUserId, cancellationToken);
                return true;
            }, cancellationToken);
            return;
        }
        await InsertAsync(messageId, consumerType, recipient, subject, body, subjectUserId, cancellationToken);
    }

    private async Task InsertAsync(Guid messageId, string consumerType, string recipient, string subject, string body,
        Guid? subjectUserId, CancellationToken cancellationToken)
    {
        var delivery = EmailDelivery.Create(
            messageId,
            consumerType,
            recipient,
            subject,
            _bodyProtector.Protect(body),
            subjectUserId);

        // The unique key is enforced atomically in PostgreSQL. A check-then-insert
        // sequence would still allow two replicas to enqueue the same event.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "EmailDeliveries"
                ("Id", "MessageId", "SubjectUserId", "ConsumerType", "Recipient", "Subject", "Body",
                 "Status", "AttemptCount", "CreatedAt", "NextAttemptAt")
            VALUES
                ({delivery.Id}, {delivery.MessageId}, {delivery.SubjectUserId}, {delivery.ConsumerType}, {delivery.Recipient},
                 {delivery.Subject}, {delivery.Body}, {(int)delivery.Status}, {delivery.AttemptCount},
                 {delivery.CreatedAt}, {delivery.NextAttemptAt})
            ON CONFLICT ("MessageId", "ConsumerType") DO NOTHING
            """, cancellationToken);
    }
}
