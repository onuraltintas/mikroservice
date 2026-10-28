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
            .Select(x => new { x.Id, x.Email, x.FirstName, x.LastName, x.IsActive, x.PhoneNumber, x.PhoneConfirmed }).ToListAsync(ct);
        return users.Select(x => new NotificationRecipient(x.Id,
            x.IsActive && !string.IsNullOrWhiteSpace(x.Email) && !x.Email.EndsWith("@deleted.invalid", StringComparison.OrdinalIgnoreCase) ? x.Email.Trim().ToLowerInvariant() : null,
            x.FirstName, x.LastName, x.IsActive && x.PhoneConfirmed ? Phone(x.PhoneNumber) : null)).ToArray();
    }

    /// <summary>Turkish mobile numbers become +90…; other numbers must already be in E.164 form.</summary>
    public static string? Phone(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 10 && digits[0] == '5') return "+90" + digits;
        if (digits.Length == 11 && digits.StartsWith("05", StringComparison.Ordinal)) return "+9" + digits;
        if (digits.Length == 12 && digits.StartsWith("905", StringComparison.Ordinal)) return "+" + digits;
        return value?.Trim().StartsWith('+') == true && digits.Length is >= 8 and <= 15 ? "+" + digits : null;
    }
}
