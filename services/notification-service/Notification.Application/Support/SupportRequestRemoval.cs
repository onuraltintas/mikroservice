using Microsoft.EntityFrameworkCore;
using Notification.Application.Commands.ReplyToSupportRequest;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;

namespace Notification.Application.Support;

/// <summary>
/// Deletes support requests together with every copy of the sender's data this service
/// holds: the staff in-app notifications (name, address and message), the pending Identity
/// forward and the acknowledgement/reply e-mails.
/// </summary>
public static class SupportRequestRemoval
{
    /// <summary>Answered requests are deleted this long after they were answered.</summary>
    public const int AnsweredRetentionMonths = 6;

    /// <summary>Requests nobody answered are deleted this long after they arrived.</summary>
    public const int OpenRetentionMonths = 3;

    public const string AcknowledgementConsumerType = "SupportAcknowledgement";
    public const string ReplyConsumerType = "SupportReply";
    public const string StaffNotificationType = "SupportRequest";

    private const int RetentionBatchSize = 500;

    /// <summary>Returns the number of deleted support requests (0 when it did not exist).</summary>
    public static Task<int> DeleteAsync(INotificationDbContext db, Guid supportRequestId, CancellationToken cancellationToken) =>
        DeleteWhereAsync(db, db.SupportRequests.Where(x => x.Id == supportRequestId), cancellationToken);

    /// <summary>
    /// Deletes requests answered more than six months ago and unanswered requests older
    /// than three months.
    /// </summary>
    public static async Task<int> DeleteExpiredAsync(INotificationDbContext db, DateTime now, CancellationToken cancellationToken)
    {
        var answeredCutoff = now.AddMonths(-AnsweredRetentionMonths);
        var openCutoff = now.AddMonths(-OpenRetentionMonths);
        var answered = await DeleteWhereAsync(db, db.SupportRequests
            .Where(x => x.IsProcessed && (x.UpdatedAt ?? x.CreatedAt) < answeredCutoff)
            .OrderBy(x => x.UpdatedAt)
            .Take(RetentionBatchSize), cancellationToken);
        var open = await DeleteWhereAsync(db, db.SupportRequests
            .Where(x => !x.IsProcessed && x.CreatedAt < openCutoff)
            .OrderBy(x => x.CreatedAt)
            .Take(RetentionBatchSize), cancellationToken);
        return answered + open;
    }

    private static async Task<int> DeleteWhereAsync(
        INotificationDbContext db,
        IQueryable<SupportRequest> selection,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var requests = await selection
            .Select(x => new { x.Id, x.Email, x.AdminNote })
            .ToListAsync(cancellationToken);
        if (requests.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return 0;
        }

        var ids = requests.Select(x => x.Id).ToList();
        var relatedEntityIds = ids.Select(id => id.ToString()).ToList();
        // Acknowledgements use the request id as message id; the latest reply's id is
        // derived from its text, which the request keeps as AdminNote.
        var emailMessageIds = ids
            .Concat(requests.Where(x => !string.IsNullOrEmpty(x.AdminNote))
                .Select(x => ReplyToSupportRequestHandler.ReplyMessageId(x.Id, x.AdminNote!)))
            .ToList();
        var addresses = requests.Select(x => x.Email).Distinct().ToList();

        await db.Notifications
            .Where(x => x.Type == StaffNotificationType && relatedEntityIds.Contains(x.RelatedEntityId!))
            .ExecuteDeleteAsync(cancellationToken);
        await db.SupportForwardDeliveries
            .Where(x => ids.Contains(x.SupportRequestId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.EmailDeliveries
            .Where(x => (x.ConsumerType == AcknowledgementConsumerType || x.ConsumerType == ReplyConsumerType)
                && emailMessageIds.Contains(x.MessageId))
            .ExecuteDeleteAsync(cancellationToken);
        var deleted = await db.SupportRequests
            .Where(x => ids.Contains(x.Id))
            .ExecuteDeleteAsync(cancellationToken);

        // Earlier replies to the same address cannot be matched by id; once the address
        // has no support request left, its remaining support e-mails go too.
        await db.EmailDeliveries
            .Where(x => (x.ConsumerType == AcknowledgementConsumerType || x.ConsumerType == ReplyConsumerType)
                && addresses.Contains(x.Recipient)
                && !db.SupportRequests.Any(request => request.Email == x.Recipient))
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }
}
