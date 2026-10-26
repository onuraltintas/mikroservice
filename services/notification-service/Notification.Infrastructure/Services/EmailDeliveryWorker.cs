using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

public sealed class EmailDeliveryWorker : LeasedDeliveryWorker<EmailDeliveryWorker.EmailDeliveryWorkItem>
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SentBodyRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan FailedBodyRetention = TimeSpan.FromDays(30);
    // Rows themselves (recipient, subject, error) and finished support forwards are
    // deleted after this; the tombstone only has to outlive any message redelivery.
    public static readonly TimeSpan DeliveryRecordRetention = TimeSpan.FromDays(180);
    // Read in-app notifications older than this are deleted.
    public static readonly TimeSpan ReadNotificationRetention = TimeSpan.FromDays(365);

    private readonly IDataProtector _bodyProtector;
    private DateTime _lastCleanupAt = DateTime.MinValue;

    public EmailDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailDeliveryWorker> logger,
        IDataProtectionProvider dataProtectionProvider)
        : base(scopeFactory, logger)
    {
        _bodyProtector = dataProtectionProvider.CreateProtector("EduPlatform.Notification.EmailDelivery.v1");
    }

    protected override async Task RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _lastCleanupAt < CleanupInterval) return;
        await CleanupCompletedAsync(cancellationToken);
        _lastCleanupAt = DateTime.UtcNow;
    }

    protected override async Task<EmailDeliveryWorkItem?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = ScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var pending = (int)EmailDeliveryStatus.Pending;
        var processing = (int)EmailDeliveryStatus.Processing;

        var delivery = await dbContext.EmailDeliveries
            .FromSqlInterpolated($"""
                SELECT * FROM "EmailDeliveries"
                WHERE (("Status" = {pending} AND "NextAttemptAt" <= {now})
                    OR ("Status" = {processing} AND "LeaseUntil" <= {now}))
                ORDER BY "NextAttemptAt", "CreatedAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (delivery is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var leaseToken = delivery.Claim(now.Add(LeaseDuration));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EmailDeliveryWorkItem(
            delivery.Id,
            delivery.Recipient,
            delivery.Subject,
            delivery.Body,
            delivery.AttemptCount,
            leaseToken,
            delivery.SubjectUserId);
    }

    protected override async Task DeliverAsync(EmailDeliveryWorkItem workItem, CancellationToken cancellationToken)
    {
        await using var scope = ScopeFactory.CreateAsyncScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        // Check erasure and lease ownership under the recipient lock, then release it
        // before talking to SMTP. Holding the lock (and its transaction) for the whole
        // send blocked that user's consumers and erasure for up to the SMTP timeout.
        async Task<bool> MaySendAsync()
        {
            if (workItem.SubjectUserId is { } subject && await dbContext.ErasedRecipients.AnyAsync(x => x.UserId == subject, cancellationToken))
                return false;
            return await dbContext.EmailDeliveries.AnyAsync(x => x.Id == workItem.Id
                && x.Status == EmailDeliveryStatus.Processing && x.LeaseToken == workItem.LeaseToken
                && x.LeaseUntil > DateTime.UtcNow, cancellationToken);
        }

        var maySend = workItem.SubjectUserId is { } userId
            ? await NotificationRecipientWrites.LockedAsync(dbContext, userId, MaySendAsync, cancellationToken)
            : await MaySendAsync();
        if (maySend)
            await DeliverOwnedAsyncCore(workItem, dbContext, emailService, cancellationToken);
    }

    private async Task DeliverOwnedAsyncCore(EmailDeliveryWorkItem workItem, NotificationDbContext dbContext,
        IEmailService emailService, CancellationToken cancellationToken)
    {
        try
        {
            var body = _bodyProtector.Unprotect(workItem.ProtectedBody);
            await emailService.SendEmailAsync(
                workItem.Recipient,
                workItem.Subject,
                body,
                cancellationToken);

            var sentAt = DateTime.UtcNow;
            var sentRows = await dbContext.EmailDeliveries
                .Where(x =>
                    x.Id == workItem.Id
                    && x.Status == EmailDeliveryStatus.Processing
                    && x.LeaseToken == workItem.LeaseToken
                    && x.LeaseUntil > sentAt)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, EmailDeliveryStatus.Sent)
                    .SetProperty(x => x.SentAt, sentAt)
                    .SetProperty(x => x.LeaseUntil, (DateTime?)null)
                    .SetProperty(x => x.LeaseToken, (Guid?)null)
                    .SetProperty(x => x.LastError, (string?)null), cancellationToken);
            if (sentRows == 1)
            {
                return;
            }

            Logger.LogWarning(
                "Email delivery {DeliveryId} lease was lost before marking it sent",
                workItem.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var permanentlyFailed = workItem.AttemptCount >= MaxAttempts;
            var retryAt = RetryAt(DateTime.UtcNow, workItem.AttemptCount);
            var error = Truncate(exception.Message);
            var failedRows = await dbContext.EmailDeliveries
                .Where(x =>
                    x.Id == workItem.Id
                    && x.Status == EmailDeliveryStatus.Processing
                    && x.LeaseToken == workItem.LeaseToken
                    && x.LeaseUntil > DateTime.UtcNow)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, permanentlyFailed
                        ? EmailDeliveryStatus.Failed
                        : EmailDeliveryStatus.Pending)
                    .SetProperty(x => x.NextAttemptAt, retryAt)
                    .SetProperty(x => x.LeaseUntil, (DateTime?)null)
                    .SetProperty(x => x.LeaseToken, (Guid?)null)
                    .SetProperty(x => x.LastError, error), cancellationToken);
            if (failedRows == 0)
            {
                Logger.LogWarning(
                    "Email delivery {DeliveryId} lease was lost before recording failure",
                    workItem.Id);
                return;
            }

            Logger.LogWarning(
                exception,
                "Email delivery {DeliveryId} failed on attempt {Attempt}; permanent={Permanent}",
                workItem.Id,
                workItem.AttemptCount,
                permanentlyFailed);
        }
    }

    private async Task CleanupCompletedAsync(CancellationToken cancellationToken)
    {
        await using var scope = ScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var now = DateTime.UtcNow;
        var sentCutoff = now.Subtract(SentBodyRetention);
        var failedCutoff = now.Subtract(FailedBodyRetention);

        // Keep the MessageId/ConsumerType row as an idempotency tombstone.
        // Clearing only the protected body prevents replay after retention
        // from creating a duplicate email while still removing secret data.
        var clearedSent = await dbContext.EmailDeliveries
            .Where(x =>
                x.Status == EmailDeliveryStatus.Sent
                && x.SentAt < sentCutoff
                && x.Body != string.Empty)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Body, string.Empty), cancellationToken);

        var clearedFailed = await dbContext.EmailDeliveries
            .Where(x =>
                x.Status == EmailDeliveryStatus.Failed
                && x.CreatedAt < failedCutoff
                && x.Body != string.Empty)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Body, string.Empty), cancellationToken);

        var cleared = clearedSent + clearedFailed;
        if (cleared > 0)
        {
            Logger.LogInformation("Cleared protected bodies from {Count} retained email delivery records", cleared);
        }

        var deleted = await PurgeExpiredRecordsAsync(dbContext, now, cancellationToken);
        if (deleted > 0)
        {
            Logger.LogInformation("Deleted {Count} expired delivery and notification records", deleted);
        }
    }

    public static async Task<int> PurgeExpiredRecordsAsync(
        NotificationDbContext dbContext,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var recordCutoff = now.Subtract(DeliveryRecordRetention);
        var notificationCutoff = now.Subtract(ReadNotificationRetention);
        var deliveries = await dbContext.EmailDeliveries
            .Where(x => (x.Status == EmailDeliveryStatus.Sent || x.Status == EmailDeliveryStatus.Failed)
                && x.CreatedAt < recordCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        var forwards = await dbContext.SupportForwardDeliveries
            .Where(x => (x.Status == SupportForwardDeliveryStatus.Sent || x.Status == SupportForwardDeliveryStatus.Failed)
                && x.CreatedAt < recordCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        var notifications = await dbContext.Notifications
            .Where(x => x.IsRead && x.CreatedAt < notificationCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        return deliveries + forwards + notifications;
    }

    public sealed record EmailDeliveryWorkItem(
        Guid Id,
        string Recipient,
        string Subject,
        string ProtectedBody,
        int AttemptCount,
        Guid LeaseToken,
        Guid? SubjectUserId);
}
