using EduPlatform.Shared.Contracts.Events.Privacy;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Privacy;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

public sealed class NotificationErasureExecutionService(
    NotificationDbContext context,
    TimeProvider timeProvider) : INotificationErasureExecutionService
{
    public async Task<NotificationErasureExecutionResult> ExecuteAsync(
        PersonalDataErasureExecutionRequestedV1 message,
        CancellationToken cancellationToken)
    {
        if (message.Scope != PersonalDataScope.Account)
            throw new InvalidOperationException("The erasure scope does not include Notification.");

        var existing = await context.ErasureExecutions
            .SingleOrDefaultAsync(item => item.RequestId == message.RequestId, cancellationToken);
        if (existing is not null)
            return ToResult(existing);

        var notifications = await context.Notifications
            .Where(item => item.UserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        context.Notifications.RemoveRange(notifications);
        var emailDeliveries = await context.EmailDeliveries
            .Where(item => item.SubjectUserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var supportRequests = await context.SupportRequests
            .Where(item => item.SubjectUserId == message.SubjectUserId)
            .ToListAsync(cancellationToken);
        var supportRequestIds = supportRequests.Select(item => item.Id).ToList();
        var supportForwardDeliveries = await context.SupportForwardDeliveries
            .Where(item => supportRequestIds.Contains(item.SupportRequestId))
            .ToListAsync(cancellationToken);
        context.EmailDeliveries.RemoveRange(emailDeliveries);
        context.SupportForwardDeliveries.RemoveRange(supportForwardDeliveries);
        context.SupportRequests.RemoveRange(supportRequests);
        var receipt = NotificationErasureExecutionReceipt.Complete(
            message.RequestId,
            notifications.Count + emailDeliveries.Count + supportRequests.Count
                + supportForwardDeliveries.Count,
            timeProvider.GetUtcNow().UtcDateTime);
        context.ErasureExecutions.Add(receipt);
        await context.SaveChangesAsync(cancellationToken);
        return ToResult(receipt);
    }

    private static NotificationErasureExecutionResult ToResult(
        NotificationErasureExecutionReceipt receipt) =>
        new(receipt.Id, receipt.RequestId, receipt.DeletedRecordCount, receipt.CompletedAt);
}
