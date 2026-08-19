using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

public sealed class EmailDeliveryWorker : BackgroundService
{
    private const int MaxAttempts = 12;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SentBodyRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan FailedBodyRetention = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailDeliveryWorker> _logger;
    private readonly IDataProtector _bodyProtector;

    public EmailDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailDeliveryWorker> logger,
        IDataProtectionProvider dataProtectionProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _bodyProtector = dataProtectionProvider.CreateProtector("EduPlatform.Notification.EmailDelivery.v1");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastCleanupAt = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.UtcNow - lastCleanupAt >= CleanupInterval)
                {
                    await CleanupCompletedAsync(stoppingToken);
                    lastCleanupAt = DateTime.UtcNow;
                }

                var workItem = await ClaimNextAsync(stoppingToken);
                if (workItem is not null)
                {
                    await DeliverAsync(workItem, stoppingToken);
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Email delivery worker iteration failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task<EmailDeliveryWorkItem?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
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
            leaseToken);
    }

    private async Task DeliverAsync(EmailDeliveryWorkItem workItem, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

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

            _logger.LogWarning(
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
            var delaySeconds = Math.Min(300, Math.Pow(2, Math.Min(workItem.AttemptCount, 8)));
            var retryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
            var error = exception.Message[..Math.Min(exception.Message.Length, 2_000)];
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
                _logger.LogWarning(
                    "Email delivery {DeliveryId} lease was lost before recording failure",
                    workItem.Id);
                return;
            }

            _logger.LogWarning(
                exception,
                "Email delivery {DeliveryId} failed on attempt {Attempt}; permanent={Permanent}",
                workItem.Id,
                workItem.AttemptCount,
                permanentlyFailed);
        }
    }

    private async Task CleanupCompletedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
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
            _logger.LogInformation("Cleared protected bodies from {Count} retained email delivery records", cleared);
        }
    }

    private sealed record EmailDeliveryWorkItem(
        Guid Id,
        string Recipient,
        string Subject,
        string ProtectedBody,
        int AttemptCount,
        Guid LeaseToken);
}
