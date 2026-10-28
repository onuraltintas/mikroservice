using EduPlatform.Shared.Infrastructure.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Notification.Infrastructure.Persistence;

/// <summary>Keeps the audit record but drops its client IP and user agent after the retention period.</summary>
public sealed class NotificationAdminAuditClientDataPurger(NotificationDbContext db) : IAdminAuditClientDataPurger
{
    // A set-based update: the context's append-only guard only covers tracked audit entities.
    public Task<int> ClearClientDataBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        db.AdminAuditRecords
            .Where(record => record.OccurredAt < cutoff && (record.ClientIp != null || record.UserAgent != null))
            .ExecuteUpdateAsync(update => update
                .SetProperty(record => record.ClientIp, (string?)null)
                .SetProperty(record => record.UserAgent, (string?)null), cancellationToken);
}
