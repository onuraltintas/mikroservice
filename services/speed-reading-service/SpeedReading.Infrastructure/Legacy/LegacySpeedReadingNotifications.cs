using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Notifications;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingNotifications(SpeedReadingDbContext db) : ISpeedReadingNotifications
{
    public async Task<NotificationPage> GetNotificationsAsync(
        Guid userId,
        bool? isRead,
        int? type,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Notifications
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted);

        if (isRead.HasValue)
        {
            query = isRead.Value
                ? query.Where(item => item.ReadAt.HasValue)
                : query.Where(item => !item.ReadAt.HasValue);
        }

        if (type.HasValue)
        {
            query = query.Where(item => item.Type == type.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(item => item.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(item => item.CreatedAt <= toDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new NotificationPage(
            rows.Select(ToSummary).ToList(),
            totalCount,
            page,
            size);
    }

    public async Task<UnreadNotificationCount> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var unread = db.Notifications
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted && !item.ReadAt.HasValue);

        return new UnreadNotificationCount(
            await unread.CountAsync(cancellationToken),
            await unread.CountAsync(item => item.Priority == 3, cancellationToken),
            await unread.CountAsync(item => item.Priority == 4, cancellationToken));
    }

    public async Task<IReadOnlyList<NotificationPreferenceSummary>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var existing = await db.NotificationTypePreferences
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        return Enumerable.Range(1, 16)
            .Select(type => existing.FirstOrDefault(item => item.NotificationType == type) is { } item
                ? new NotificationPreferenceSummary(
                    item.Id,
                    item.NotificationType,
                    item.EnableInApp,
                    item.EnableEmail,
                    item.EnablePush,
                    item.PreferredTime)
                : new NotificationPreferenceSummary(Guid.Empty, type, true, true, false, null))
            .ToList();
    }

    public async Task UpdatePreferencesAsync(
        Guid userId,
        IReadOnlyList<NotificationPreferenceSummary> preferences,
        CancellationToken cancellationToken)
    {
        var existing = await db.NotificationTypePreferences
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var preference in preferences)
        {
            if (preference.NotificationType is < 1 or > 16)
            {
                throw new ArgumentException("NotificationType must be between 1 and 16.");
            }

            var row = existing.FirstOrDefault(item => item.NotificationType == preference.NotificationType);
            if (row is null)
            {
                db.NotificationTypePreferences.Add(new LegacyNotificationTypePreference
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    NotificationType = preference.NotificationType,
                    EnableInApp = preference.EnableInApp,
                    EnableEmail = preference.EnableEmail,
                    EnablePush = preference.EnablePush,
                    PreferredTime = Normalize(preference.PreferredTime),
                    CreatedAt = DateTime.UtcNow
                });
                continue;
            }

            row.EnableInApp = preference.EnableInApp;
            row.EnableEmail = preference.EnableEmail;
            row.EnablePush = preference.EnablePush;
            row.PreferredTime = Normalize(preference.PreferredTime);
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> SubscribePushAsync(
        Guid userId,
        SubscribePushRequest request,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Endpoint must be a valid URL.");
        }

        var endpoint = request.Endpoint.Trim();
        var row = await db.PushSubscriptions
            .FirstOrDefaultAsync(item => item.Endpoint == endpoint, cancellationToken);

        if (row is null)
        {
            row = new LegacyPushSubscription
            {
                Id = Guid.NewGuid(),
                Endpoint = endpoint,
                CreatedAt = DateTime.UtcNow
            };
            db.PushSubscriptions.Add(row);
        }

        row.UserId = userId;
        row.P256DH = request.P256DH?.Trim() ?? string.Empty;
        row.Auth = request.Auth?.Trim() ?? string.Empty;
        row.UserAgent = Normalize(userAgent);
        row.IsActive = true;
        row.IsDeleted = false;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var row = await db.Notifications
            .SingleOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (row is null)
        {
            return false;
        }

        row.ReadAt ??= DateTime.UtcNow;
        row.Status = 4;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.Notifications
            .Where(item => item.UserId == userId && !item.IsDeleted && !item.ReadAt.HasValue)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.ReadAt = now;
            row.Status = 4;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var row = await db.Notifications
            .SingleOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (row is null)
        {
            return false;
        }

        row.IsDeleted = true;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<NotificationSummary> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMessage(request.Title, request.Message, request.Type, request.Priority);
        var now = DateTime.UtcNow;
        var row = new LegacyUserNotification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Type = request.Type,
            Priority = request.Priority,
            Channel = 3,
            Status = 2,
            ActionUrl = Normalize(request.ActionUrl),
            IconUrl = Normalize(request.IconUrl),
            SentAt = now,
            CreatedAt = now
        };
        db.Notifications.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(row);
    }

    public async Task<AdminNotificationPage> GetAllAsync(
        Guid? userId,
        int? type,
        bool? isRead,
        string? userRole,
        DateTime? fromDate,
        DateTime? toDate,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Notifications
            .AsNoTracking()
            .Where(item => !item.IsDeleted);

        if (userId.HasValue) query = query.Where(item => item.UserId == userId.Value);
        if (type.HasValue) query = query.Where(item => item.Type == type.Value);
        if (isRead.HasValue)
        {
            query = isRead.Value
                ? query.Where(item => item.ReadAt.HasValue)
                : query.Where(item => !item.ReadAt.HasValue);
        }
        if (!string.IsNullOrWhiteSpace(userRole)) query = query.Where(item => item.UserRole == userRole);
        if (fromDate.HasValue) query = query.Where(item => item.CreatedAt >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(item => item.CreatedAt <= toDate.Value);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(item => item.Title.Contains(term) || item.Message.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new AdminNotificationPage(
            rows.Select(ToAdminSummary).ToList(),
            totalCount,
            page,
            size);
    }

    public async Task<BulkNotificationResult> SendBulkAsync(
        BulkNotificationRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMessage(request.Title, request.Message, request.Type, request.Priority);
        var targetUserIds = await GetTargetUserIdsAsync(request.TargetType, request.TargetRole, cancellationToken);
        var errors = new List<string>();
        var now = DateTime.UtcNow;

        foreach (var userId in targetUserIds)
        {
            db.Notifications.Add(new LegacyUserNotification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = request.Title.Trim(),
                Message = request.Message.Trim(),
                Type = request.Type,
                Priority = request.Priority,
                Channel = 3,
                Status = 2,
                ActionUrl = Normalize(request.ActionUrl),
                SentAt = now,
                CreatedAt = now
            });
        }

        if (request.SendEmail)
        {
            errors.Add("Email delivery is not configured; only in-app notifications were created.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return new BulkNotificationResult(
            !request.SendEmail,
            targetUserIds.Count,
            0,
            0,
            errors);
    }

    private async Task<List<Guid>> GetTargetUserIdsAsync(
        string targetType,
        string? targetRole,
        CancellationToken cancellationToken)
    {
        if (string.Equals(targetType, "Role", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(targetRole))
            {
                throw new ArgumentException("TargetRole is required when TargetType is Role.");
            }

            return await (
                from link in db.UserRoleLinks.AsNoTracking()
                join role in db.Roles.AsNoTracking() on link.RoleId equals role.Id
                join user in db.Users.AsNoTracking() on link.UserId equals user.Id
                where !user.IsDeleted && role.Name == targetRole
                select user.Id).Distinct().ToListAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(targetType)
            && !string.Equals(targetType, "All", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("TargetType must be All or Role.");
        }

        return await db.Users
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    private static NotificationSummary ToSummary(LegacyUserNotification row) =>
        new(
            row.Id,
            row.UserId,
            row.Title,
            row.Message,
            row.Type,
            NotificationTypeName(row.Type),
            row.Priority,
            PriorityName(row.Priority),
            row.Status,
            row.ActionUrl,
            row.IconUrl,
            row.CreatedAt,
            row.ReadAt);

    private static AdminNotificationSummary ToAdminSummary(LegacyUserNotification row) =>
        new(
            row.Id,
            row.UserId,
            row.Title,
            row.Message,
            row.Type,
            NotificationTypeName(row.Type),
            row.Priority,
            PriorityName(row.Priority),
            row.Status,
            row.ActionUrl,
            row.IconUrl,
            row.CreatedAt,
            row.ReadAt,
            row.UserName ?? string.Empty,
            row.UserEmail ?? string.Empty,
            row.UserRole ?? string.Empty);

    private static void ValidateMessage(string title, string message, int type, int priority)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ArgumentException("Title is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 1000)
            throw new ArgumentException("Message is required and must not exceed 1000 characters.");
        if (type is < 1 or > 16)
            throw new ArgumentException("Type must be between 1 and 16.");
        if (priority is < 1 or > 4)
            throw new ArgumentException("Priority must be between 1 and 4.");
    }

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NotificationTypeName(int type) => type switch
    {
        1 => "NewAssignment",
        2 => "AssignmentDueSoon",
        3 => "AssignmentOverdue",
        4 => "ExerciseCompleted",
        5 => "MilestoneAchieved",
        6 => "WeeklyProgress",
        7 => "MonthlyProgress",
        8 => "DailyReminder",
        9 => "SystemAnnouncement",
        10 => "TeacherFeedback",
        11 => "AchievementUnlocked",
        12 => "GoalCompleted",
        13 => "StudentActivitySummary",
        14 => "StudentProgramCompleted",
        15 => "NewUserRegistered",
        16 => "SystemError",
        _ => $"Type{type}"
    };

    private static string PriorityName(int priority) => priority switch
    {
        1 => "Low",
        2 => "Normal",
        3 => "High",
        4 => "Urgent",
        _ => $"Priority{priority}"
    };
}
