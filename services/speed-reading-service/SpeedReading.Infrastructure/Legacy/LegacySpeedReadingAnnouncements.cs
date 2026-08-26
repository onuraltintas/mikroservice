using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Notifications;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingAnnouncements(SpeedReadingDbContext db) : ISpeedReadingAnnouncements
{
    public async Task<IReadOnlyList<AnnouncementSummary>> GetMyAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        Guid? institutionId,
        bool includeDismissed,
        bool onlyPinned,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rows = await db.Announcements
            .AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted
                && (!item.StartDate.HasValue || item.StartDate <= now)
                && (!(item.ExpiresAt ?? item.EndDate).HasValue || (item.ExpiresAt ?? item.EndDate) >= now)
                && (!onlyPinned || item.IsPinned))
            .OrderByDescending(item => item.IsPinned)
            .ThenByDescending(item => item.Priority)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        rows = rows.Where(item => AudienceMatches(item, roles, institutionId)).ToList();
        var ids = rows.Select(item => item.Id).ToList();
        var interactions = await db.AnnouncementUserInteractions
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted && ids.Contains(item.AnnouncementId))
            .ToDictionaryAsync(item => item.AnnouncementId, cancellationToken);

        return rows
            .Where(item => includeDismissed || !IsDismissed(interactions, item.Id))
            .Select(item => ToSummary(item, interactions.GetValueOrDefault(item.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<AnnouncementDetail>> GetAllAsync(
        bool? isActive,
        bool? isPinned,
        int? targetAudience,
        Guid? targetInstitutionId,
        bool includeExpired,
        int? take,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = db.Announcements
            .AsNoTracking()
            .Where(item => !item.IsDeleted);

        if (isActive.HasValue) query = query.Where(item => item.IsActive == isActive.Value);
        if (isPinned.HasValue) query = query.Where(item => item.IsPinned == isPinned.Value);
        if (targetInstitutionId.HasValue) query = query.Where(item => item.TargetInstitutionId == targetInstitutionId.Value);
        if (!includeExpired)
        {
            query = query.Where(item => !(item.ExpiresAt ?? item.EndDate).HasValue || (item.ExpiresAt ?? item.EndDate) >= now);
        }

        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        if (targetAudience.HasValue)
        {
            rows = rows.Where(item => ParseAudience(item.TargetAudience) == targetAudience.Value).ToList();
        }

        rows = rows.Take(Math.Clamp(take ?? 100, 1, 500)).ToList();

        var ids = rows.Select(item => item.Id).ToList();
        var interactions = await db.AnnouncementUserInteractions
            .AsNoTracking()
            .Where(item => !item.IsDeleted && ids.Contains(item.AnnouncementId))
            .ToListAsync(cancellationToken);
        var mine = interactions
            .Where(item => item.UserId == userId)
            .ToDictionary(item => item.AnnouncementId);

        return rows.Select(item => ToDetail(
            item,
            interactions.Where(interaction => interaction.AnnouncementId == item.Id).ToList(),
            mine.GetValueOrDefault(item.Id))).ToList();
    }

    public async Task<AnnouncementStats?> GetStatsAsync(Guid id, CancellationToken cancellationToken)
    {
        var exists = await db.Announcements
            .AsNoTracking()
            .AnyAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (!exists) return null;

        var interactions = await db.AnnouncementUserInteractions
            .AsNoTracking()
            .Where(item => item.AnnouncementId == id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var views = interactions.Where(item => item.ViewedAt.HasValue).ToList();
        var clicks = interactions.Where(item => item.ClickedAt.HasValue).ToList();
        var dismissals = interactions.Where(item => item.DismissedAt.HasValue).ToList();
        var viewCount = views.Count;

        return new AnnouncementStats(
            id,
            viewCount,
            viewCount,
            clicks.Count,
            clicks.Count,
            dismissals.Count,
            0,
            viewCount == 0 ? 0 : Math.Round((decimal)clicks.Count / viewCount * 100, 2),
            viewCount == 0 ? 0 : Math.Round((decimal)dismissals.Count / viewCount * 100, 2),
            views.MinBy(item => item.ViewedAt)?.ViewedAt,
            views.MaxBy(item => item.ViewedAt)?.ViewedAt);
    }

    public async Task<Guid> CreateAsync(Guid userId, CreateAnnouncementRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Title, request.Content, request.Priority, request.TargetAudience, request.DisplayType);
        var now = DateTime.UtcNow;
        var expiration = request.ExpiresAt;
        var row = new LegacyAnnouncement
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Content = request.Content,
            PlainTextContent = Normalize(request.PlainTextContent),
            Type = DisplayTypeName(request.DisplayType),
            DisplayType = request.DisplayType,
            Priority = request.Priority,
            TargetAudience = AudienceName(request.TargetAudience),
            TargetInstitutionId = request.TargetInstitutionId,
            TargetRoles = SerializeRoles(request.TargetRoles),
            StartDate = request.StartDate,
            EndDate = expiration,
            ExpiresAt = expiration,
            IsPinned = request.IsPinned,
            IsActive = true,
            ActionUrl = Normalize(request.ActionUrl),
            ActionText = Normalize(request.ActionText),
            Icon = Normalize(request.Icon),
            ImageUrl = Normalize(request.Icon),
            ColorTheme = Normalize(request.ColorTheme),
            SendEmailNotification = request.SendEmailNotification,
            CreateInAppNotification = request.CreateInAppNotification,
            CreatedByUserId = userId,
            CreatedAt = now
        };

        db.Announcements.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateAnnouncementRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Title, request.Content, request.Priority, request.TargetAudience, request.DisplayType);
        var row = await db.Announcements
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;

        row.Title = request.Title.Trim();
        row.Content = request.Content;
        row.PlainTextContent = Normalize(request.PlainTextContent);
        row.Type = DisplayTypeName(request.DisplayType);
        row.DisplayType = request.DisplayType;
        row.Priority = request.Priority;
        row.TargetAudience = AudienceName(request.TargetAudience);
        row.TargetInstitutionId = request.TargetInstitutionId;
        row.TargetRoles = SerializeRoles(request.TargetRoles);
        row.StartDate = request.StartDate;
        row.EndDate = request.ExpiresAt;
        row.ExpiresAt = request.ExpiresAt;
        row.IsPinned = request.IsPinned;
        row.IsActive = request.IsActive;
        row.ActionUrl = Normalize(request.ActionUrl);
        row.ActionText = Normalize(request.ActionText);
        row.Icon = Normalize(request.Icon);
        row.ImageUrl = Normalize(request.Icon);
        row.ColorTheme = Normalize(request.ColorTheme);
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Announcements
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;
        row.IsDeleted = true;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> RecordViewAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        RecordInteractionAsync(userId, id, static item => item.ViewedAt ??= DateTime.UtcNow, cancellationToken);

    public Task<bool> RecordClickAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        RecordInteractionAsync(userId, id, static item => item.ClickedAt ??= DateTime.UtcNow, cancellationToken);

    public Task<bool> DismissAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        RecordInteractionAsync(userId, id, static item => item.DismissedAt ??= DateTime.UtcNow, cancellationToken);

    private async Task<bool> RecordInteractionAsync(
        Guid userId,
        Guid announcementId,
        Action<LegacyAnnouncementUserInteraction> update,
        CancellationToken cancellationToken)
    {
        var exists = await db.Announcements
            .AnyAsync(item => item.Id == announcementId && !item.IsDeleted, cancellationToken);
        if (!exists) return false;

        var interaction = await db.AnnouncementUserInteractions
            .SingleOrDefaultAsync(item => item.AnnouncementId == announcementId && item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (interaction is null)
        {
            interaction = new LegacyAnnouncementUserInteraction
            {
                Id = Guid.NewGuid(),
                AnnouncementId = announcementId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            db.AnnouncementUserInteractions.Add(interaction);
        }

        update(interaction);
        interaction.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static AnnouncementSummary ToSummary(LegacyAnnouncement row, LegacyAnnouncementUserInteraction? interaction) =>
        new(
            row.Id,
            row.Title,
            row.Content,
            row.PlainTextContent,
            row.Priority,
            ParseDisplayType(row.DisplayType, row.Type),
            row.Icon ?? row.ImageUrl,
            row.ColorTheme,
            row.ActionUrl,
            row.ActionText,
            row.IsPinned,
            row.StartDate,
            row.ExpiresAt ?? row.EndDate,
            row.CreatedAt,
            interaction?.ViewedAt.HasValue == true,
            interaction?.DismissedAt.HasValue == true,
            interaction?.ClickedAt.HasValue == true);

    private static AnnouncementDetail ToDetail(
        LegacyAnnouncement row,
        IReadOnlyCollection<LegacyAnnouncementUserInteraction> interactions,
        LegacyAnnouncementUserInteraction? mine)
    {
        return new AnnouncementDetail(
            row.Id,
            row.Title,
            row.Content,
            row.PlainTextContent,
            row.Priority,
            ParseDisplayType(row.DisplayType, row.Type),
            row.Icon ?? row.ImageUrl,
            row.ColorTheme,
            row.ActionUrl,
            row.ActionText,
            row.IsPinned,
            row.StartDate,
            row.ExpiresAt ?? row.EndDate,
            row.CreatedAt,
            mine?.ViewedAt.HasValue == true,
            mine?.DismissedAt.HasValue == true,
            mine?.ClickedAt.HasValue == true,
            ParseAudience(row.TargetAudience),
            row.TargetInstitutionId,
            ParseRoles(row.TargetRoles),
            row.IsActive,
            row.SendEmailNotification,
            row.CreateInAppNotification,
            row.EmailCampaignId,
            interactions.Count(item => item.ViewedAt.HasValue),
            interactions.Count(item => item.ClickedAt.HasValue),
            row.UpdatedAt,
            row.CreatedByUserId);
    }

    private static bool AudienceMatches(LegacyAnnouncement row, IReadOnlyCollection<string> roles, Guid? institutionId)
    {
        var audience = ParseAudience(row.TargetAudience);
        if (audience == 1) return true;
        if (audience == 5) return row.TargetInstitutionId.HasValue && row.TargetInstitutionId == institutionId;
        if (audience == 2) return roles.Contains("Student", StringComparer.OrdinalIgnoreCase);
        if (audience == 3) return roles.Contains("Teacher", StringComparer.OrdinalIgnoreCase);
        if (audience == 4) return roles.Any(role => role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("InstitutionAdmin", StringComparison.OrdinalIgnoreCase));

        var targetRoles = ParseRoles(row.TargetRoles);
        return targetRoles.Count == 0 || targetRoles.Any(target => roles.Contains(target, StringComparer.OrdinalIgnoreCase));
    }

    private static void Validate(string title, string content, int priority, int audience, int displayType)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ArgumentException("Title is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required.");
        if (priority is < 1 or > 4) throw new ArgumentException("Priority must be between 1 and 4.");
        if (audience is < 1 or > 6) throw new ArgumentException("TargetAudience is invalid.");
        if (displayType is < 1 or > 5) throw new ArgumentException("DisplayType is invalid.");
    }

    private static int ParseAudience(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "1" or "all" => 1,
        "2" or "students" or "student" => 2,
        "3" or "teachers" or "teacher" => 3,
        "4" or "admins" or "admin" => 4,
        "5" or "institution" => 5,
        "6" or "custom" => 6,
        _ => 1
    };

    private static string AudienceName(int audience) => audience switch
    {
        2 => "Students",
        3 => "Teachers",
        4 => "Admins",
        5 => "Institution",
        6 => "Custom",
        _ => "All"
    };

    private static int ParseDisplayType(int stored, string? legacyType) => stored is >= 1 and <= 5
        ? stored == 1 && !string.Equals(legacyType?.Trim(), "Banner", StringComparison.OrdinalIgnoreCase)
            ? ParseLegacyDisplayType(legacyType)
            : stored
        : ParseLegacyDisplayType(legacyType);

    private static int ParseLegacyDisplayType(string? legacyType) => legacyType?.Trim().ToLowerInvariant() switch
        {
            "2" or "modal" => 2,
            "3" or "notification" => 3,
            "4" or "toast" => 4,
            "5" or "sidebar" => 5,
            _ => 1
        };

    private static bool IsDismissed(
        IReadOnlyDictionary<Guid, LegacyAnnouncementUserInteraction> interactions,
        Guid announcementId) =>
        interactions.TryGetValue(announcementId, out var interaction) && interaction.DismissedAt.HasValue;

    private static string DisplayTypeName(int displayType) => displayType switch
    {
        2 => "Modal",
        3 => "Notification",
        4 => "Toast",
        5 => "Sidebar",
        _ => "Banner"
    };

    private static IReadOnlyList<string> ParseRoles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            var json = JsonSerializer.Deserialize<string[]>(value);
            if (json is not null) return json.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()).ToList();
        }
        catch (JsonException)
        {
            // Older rows use comma-separated roles.
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? SerializeRoles(IReadOnlyList<string>? roles)
    {
        var normalized = roles?.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return normalized is { Length: > 0 } ? JsonSerializer.Serialize(normalized) : null;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
