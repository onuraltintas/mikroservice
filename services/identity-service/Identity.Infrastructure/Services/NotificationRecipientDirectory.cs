using Identity.Application.Notifications;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

/// <summary>Roadmap O8: recipient names and addresses for the notification service; inactive or erased accounts get no address.</summary>
public sealed class NotificationRecipientDirectory(IdentityDbContext db) : INotificationRecipientDirectory
{
    public async Task<IReadOnlyList<NotificationRecipient>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (ids.Length > INotificationRecipientDirectory.MaxUsers) throw new ArgumentException("En fazla 200 alıcı sorgulanabilir.");
        if (ids.Length == 0) return [];
        var users = await db.Users.AsNoTracking().Where(x => Enumerable.Contains(ids, x.Id))
            .Select(x => new { x.Id, x.Email, x.FirstName, x.LastName, x.IsActive }).ToListAsync(ct);
        return users.Select(x => new NotificationRecipient(x.Id,
            x.IsActive && !string.IsNullOrWhiteSpace(x.Email) && !x.Email.EndsWith("@deleted.invalid", StringComparison.OrdinalIgnoreCase) ? x.Email.Trim().ToLowerInvariant() : null,
            x.FirstName, x.LastName)).ToArray();
    }
}
