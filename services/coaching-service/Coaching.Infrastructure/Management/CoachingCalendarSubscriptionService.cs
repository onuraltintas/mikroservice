using System.Security.Cryptography;
using System.Text;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetCalendarFeed;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed record CoachingCalendarSubscriptionStatus(string Role, bool Active, DateTime? CreatedAt, DateTime? LastUsedAt);
/// <summary>The new link; the path is shown once (only its hash is kept).</summary>
public sealed record CoachingCalendarSubscriptionLink(string Role, string Path, DateTime CreatedAt);

/// <summary>
/// Roadmap O6: a private calendar link Google or Apple Calendar subscribes to. The link holds a random 256-bit token;
/// only its SHA-256 hash is stored. Creating a link replaces the previous one; turning it off deletes it.
/// </summary>
public sealed class CoachingCalendarSubscriptionService(CoachingDbContext db, ICoachingCalendarRepository calendar, TimeProvider clock,
    Coaching.Application.Profiles.ICoachingAccountDirectory accounts)
{
    public const string FeedPathPrefix = "/api/calendar/feed/";
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<CoachingCalendarSubscriptionStatus> GetAsync(Guid userId, string role, CancellationToken ct)
    {
        role = Role(role);
        var feed = await db.CoachingCalendarFeeds.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.Role == role, ct);
        return new(role, feed is not null, feed?.CreatedAt, feed?.LastUsedAt);
    }

    public async Task<CoachingCalendarSubscriptionLink> CreateAsync(Guid userId, string role, CancellationToken ct)
    {
        role = Role(role);
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı gerekli.", nameof(userId));
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var feed = await db.CoachingCalendarFeeds.SingleOrDefaultAsync(x => x.UserId == userId && x.Role == role, ct);
        if (feed is null) { feed = new CoachingCalendarFeed { UserId = userId, Role = role }; db.CoachingCalendarFeeds.Add(feed); }
        feed.TokenHash = Hash(token); feed.CreatedAt = Now; feed.LastUsedAt = null;
        await db.SaveChangesAsync(ct);
        return new(role, $"{FeedPathPrefix}{token}.ics", feed.CreatedAt);
    }

    public async Task RevokeAsync(Guid userId, string role, CancellationToken ct)
    {
        role = Role(role);
        var feed = await db.CoachingCalendarFeeds.SingleOrDefaultAsync(x => x.UserId == userId && x.Role == role, ct);
        if (feed is null) return;
        db.CoachingCalendarFeeds.Remove(feed);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The iCalendar text for a link token, or null when the token is unknown or was replaced.</summary>
    public async Task<string?> FeedAsync(string token, CancellationToken ct)
    {
        token = (token ?? string.Empty).Trim();
        if (token.EndsWith(".ics", StringComparison.OrdinalIgnoreCase)) token = token[..^4];
        if (token.Length != 43 || token.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return null;
        var hash = Hash(token);
        var feed = await db.CoachingCalendarFeeds.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (feed is null || !await OwnerIsActiveAsync(feed, ct)) return null;
        var from = Now.AddDays(-30); var to = Now.AddDays(180);
        var sessions = feed.Role == "Teacher"
            ? await calendar.GetByTeacherIdAsync(feed.UserId, from, to, 500, ct)
            : await calendar.GetByStudentIdAsync(feed.UserId, from, to, 500, ct);
        // Calendar apps poll often; the last use is recorded at most once an hour.
        if (feed.LastUsedAt is null || Now - feed.LastUsedAt > TimeSpan.FromHours(1))
        {
            feed.LastUsedAt = Now;
            await db.SaveChangesAsync(ct);
        }
        return CalendarFeedFormatter.Format(sessions, "Eduİvme Koçluk");
    }

    /// <summary>
    /// K2 (31.10.2026): a link only works while its owner is still an active coaching teacher or student; a deactivated or
    /// removed account stops receiving session titles and meeting links.
    /// </summary>
    private async Task<bool> OwnerIsActiveAsync(CoachingCalendarFeed feed, CancellationToken ct)
    {
        var account = (await accounts.GetAccountsAsync([feed.UserId], ct)).SingleOrDefault(x => x.UserId == feed.UserId);
        if (account is null || !account.IsEligible(feed.Role)) return false;
        return feed.Role == "Teacher"
            ? await db.CoachingTeacherProfiles.AsNoTracking().AnyAsync(x => x.UserId == feed.UserId && x.IsActive, ct)
            : await db.CoachingStudentProfiles.AsNoTracking().AnyAsync(x => x.UserId == feed.UserId && x.IsActive, ct);
    }

    private static string Role(string role) => role?.Trim().ToLowerInvariant() switch
    {
        "teacher" => "Teacher",
        "student" => "Student",
        _ => throw new ArgumentException("Takvim bağlantısı öğretmen ya da öğrenci için olabilir.", nameof(role))
    };

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))).ToLowerInvariant();
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
