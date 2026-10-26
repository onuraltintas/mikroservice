using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Notification.Application.Commands.SubmitSupportRequest;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

public sealed class SupportForwardDeliveryWorker : LeasedDeliveryWorker<SupportForwardDeliveryWorker.SupportForwardWorkItem>
{
    public SupportForwardDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SupportForwardDeliveryWorker> logger)
        : base(scopeFactory, logger)
    {
    }

    protected override async Task<SupportForwardWorkItem?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = ScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var pending = (int)SupportForwardDeliveryStatus.Pending;
        var processing = (int)SupportForwardDeliveryStatus.Processing;
        var delivery = await dbContext.SupportForwardDeliveries
            .FromSqlInterpolated($"""
                SELECT * FROM "SupportForwardDeliveries"
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

        return new SupportForwardWorkItem(
            delivery.Id,
            delivery.SupportRequestId,
            delivery.AttemptCount,
            leaseToken);
    }

    protected override async Task DeliverAsync(SupportForwardWorkItem workItem, CancellationToken cancellationToken)
    {
        await using var scope = ScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityInternalService>();
        var supportRequest = await dbContext.SupportRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workItem.SupportRequestId, cancellationToken);

        if (supportRequest is null)
        {
            await MarkFailureAsync(
                workItem,
                "Support request was not found.",
                permanentlyFailed: true,
                cancellationToken: cancellationToken);
            return;
        }

        var command = new SubmitSupportRequestCommand(
            supportRequest.FirstName,
            supportRequest.LastName,
            supportRequest.Email,
            supportRequest.Subject,
            supportRequest.Message,
            supportRequest.IdempotencyKey);

        try
        {
            var result = await identityService.ForwardSupportRequestAsync(
                command,
                supportRequest.Id,
                cancellationToken);
            if (result == SupportForwardResult.Sent)
            {
                var sentAt = DateTime.UtcNow;
                var sentRows = await dbContext.SupportForwardDeliveries
                    .Where(x =>
                        x.Id == workItem.Id
                        && x.Status == SupportForwardDeliveryStatus.Processing
                        && x.LeaseToken == workItem.LeaseToken
                        && x.LeaseUntil > sentAt)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, SupportForwardDeliveryStatus.Sent)
                        .SetProperty(x => x.SentAt, sentAt)
                        .SetProperty(x => x.LeaseUntil, (DateTime?)null)
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LastError, (string?)null), cancellationToken);

                if (sentRows == 1)
                    return;
            }

            // A refusal is final; transient failures stop after MaxAttempts like exceptions do.
            await MarkFailureAsync(
                workItem,
                result == SupportForwardResult.Rejected
                    ? "Identity service rejected the support forward request."
                    : "Identity service could not be reached for the support forward request.",
                permanentlyFailed: result == SupportForwardResult.Rejected || workItem.AttemptCount >= MaxAttempts,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MarkFailureAsync(
                workItem,
                exception.Message,
                permanentlyFailed: workItem.AttemptCount >= MaxAttempts,
                cancellationToken: cancellationToken);
        }
    }

    private async Task MarkFailureAsync(
        SupportForwardWorkItem workItem,
        string message,
        bool permanentlyFailed,
        CancellationToken cancellationToken)
    {
        await using var scope = ScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var retryAt = RetryAt(DateTime.UtcNow, workItem.AttemptCount);
        var error = Truncate(message);
        var updated = await dbContext.SupportForwardDeliveries
            .Where(x =>
                x.Id == workItem.Id
                && x.Status == SupportForwardDeliveryStatus.Processing
                && x.LeaseToken == workItem.LeaseToken
                && x.LeaseUntil > DateTime.UtcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, permanentlyFailed
                    ? SupportForwardDeliveryStatus.Failed
                    : SupportForwardDeliveryStatus.Pending)
                .SetProperty(x => x.NextAttemptAt, retryAt)
                .SetProperty(x => x.LeaseUntil, (DateTime?)null)
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LastError, error), cancellationToken);

        if (updated > 0)
        {
            Logger.LogWarning(
                "Support forward delivery {DeliveryId} failed; attempt={Attempt}; permanent={Permanent}",
                workItem.Id,
                workItem.AttemptCount,
                permanentlyFailed);
        }
    }

    public sealed record SupportForwardWorkItem(
        Guid Id,
        Guid SupportRequestId,
        int AttemptCount,
        Guid LeaseToken);
}
