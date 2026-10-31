using System.Security.Cryptography;
using System.Text;
using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Results;
using Identity.Application.ParentLinks;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class ParentLinkService(IdentityDbContext db, TimeProvider clock, IParentLinkEventPublisher? events = null) : IParentLinkService
{
    // No 0/O, 1/I/L: the code is read aloud or typed from a phone screen.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>The parent's own weekly-summary switch (ParentProfile.NotificationPreferences is jsonb).</summary>
    private const string WeeklySummaryOff = "{\"weeklyProgress\": false}";

    // Where each side acts on a link (V1 brief): the student's account settings on the product they use, the parent panel.
    private const string CoachingStudentPath = "/coaching-portal/account/settings/profile";
    private const string SpeedReadingStudentPath = "/veli-baglantisi";
    private const string ParentPath = "/coaching-portal/parent";

    public async Task<Result<ParentLinkCodeIssued>> CreateCodeAsync(Guid studentUserId, CancellationToken ct)
    {
        if (!await HasActiveRoleAsync(studentUserId, "Student", ct)) return Result.Failure<ParentLinkCodeIssued>(ParentLinkErrors.Forbidden);
        var now = Now;
        foreach (var old in await db.ParentLinkCodes.Where(x => x.StudentUserId == studentUserId && x.UsedAt == null && x.RevokedAt == null).ToListAsync(ct))
            old.Revoke(now);
        var code = new string(Enumerable.Range(0, 8).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
        var issued = ParentLinkCode.Issue(studentUserId, Hash(code), now);
        db.ParentLinkCodes.Add(issued);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ParentLinkCodeIssued($"{code[..4]}-{code[4..]}", issued.ExpiresAt));
    }

    public async Task<Result<ParentLinkView>> RequestAsync(Guid parentUserId, string code, ParentRelationship relationship, CancellationToken ct)
    {
        if (!await HasActiveRoleAsync(parentUserId, "Parent", ct)) return Result.Failure<ParentLinkView>(ParentLinkErrors.Forbidden);
        // Only these relationships carry the right to read a child's data (see AuthorizeCoachingAgreementRepresentativeAsync).
        if (relationship is not (ParentRelationship.Mother or ParentRelationship.Father or ParentRelationship.Guardian))
            return Result.Failure<ParentLinkView>(ParentLinkErrors.InvalidRelationship);
        var normalized = Normalize(code);
        var now = Now;
        var hash = normalized is null ? null : Hash(normalized);
        var issued = hash is null ? null : await db.ParentLinkCodes.SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
        if (issued is null || !issued.IsUsable(now) || !await HasActiveRoleAsync(issued.StudentUserId, "Student", ct))
            return Result.Failure<ParentLinkView>(ParentLinkErrors.InvalidCode);
        if (await db.ParentStudentRelationships.AnyAsync(x => x.ParentUserId == parentUserId && x.StudentUserId == issued.StudentUserId
                && x.Status != ParentStudentRelationshipStatus.Revoked, ct))
            return Result.Failure<ParentLinkView>(ParentLinkErrors.AlreadyLinked);
        var link = ParentStudentRelationship.Request(parentUserId, issued.StudentUserId, relationship, parentUserId, now);
        issued.Use(parentUserId, now);
        db.ParentStudentRelationships.Add(link);
        await NotifyAsync(link, ParentLinkChanges.Requested, toStudent: true, now, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success((await ViewsAsync(db.ParentStudentRelationships.Where(x => x.Id == link.Id), ct)).Single());
    }

    public async Task<Result<IReadOnlyList<ParentLinkView>>> ListAsync(Guid userId, CancellationToken ct)
    {
        var isStudent = await HasActiveRoleAsync(userId, "Student", ct);
        var isParent = await HasActiveRoleAsync(userId, "Parent", ct);
        if (!isStudent && !isParent) return Result.Failure<IReadOnlyList<ParentLinkView>>(ParentLinkErrors.Forbidden);
        var links = db.ParentStudentRelationships.Where(x => x.Status != ParentStudentRelationshipStatus.Revoked
            && (isStudent && x.StudentUserId == userId || isParent && x.ParentUserId == userId));
        return Result.Success(await ViewsAsync(links, ct));
    }

    public async Task<Result> ApproveAsync(Guid studentUserId, Guid linkId, CancellationToken ct)
    {
        var link = await db.ParentStudentRelationships.SingleOrDefaultAsync(x => x.Id == linkId && x.StudentUserId == studentUserId
            && x.Status != ParentStudentRelationshipStatus.Revoked, ct);
        if (link is null || !await HasActiveRoleAsync(studentUserId, "Student", ct)) return Result.Failure(ParentLinkErrors.NotFound);
        if (link.Status != ParentStudentRelationshipStatus.Pending) return Result.Failure(ParentLinkErrors.NotPending);
        var now = Now;
        link.Verify(ParentStudentVerificationMethod.InvitationAcceptance, studentUserId, now);
        await NotifyAsync(link, ParentLinkChanges.Approved, toStudent: false, now, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RevokeAsync(Guid userId, Guid linkId, CancellationToken ct)
    {
        var link = await db.ParentStudentRelationships.SingleOrDefaultAsync(x => x.Id == linkId
            && (x.StudentUserId == userId || x.ParentUserId == userId) && x.Status != ParentStudentRelationshipStatus.Revoked, ct);
        if (link is null) return Result.Failure(ParentLinkErrors.NotFound);
        var now = Now;
        var byStudent = link.StudentUserId == userId;
        link.Revoke(userId, now, byStudent ? "Öğrenci veli bağlantısını kaldırdı." : "Veli bağlantıyı kaldırdı.");
        await NotifyAsync(link, ParentLinkChanges.Revoked, toStudent: !byStudent, now, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetProductVisibilityAsync(Guid studentUserId, Guid linkId, bool coaching, bool speedReading, CancellationToken ct)
    {
        var link = await db.ParentStudentRelationships.SingleOrDefaultAsync(x => x.Id == linkId && x.StudentUserId == studentUserId
            && x.Status != ParentStudentRelationshipStatus.Revoked, ct);
        if (link is null || !await HasActiveRoleAsync(studentUserId, "Student", ct)) return Result.Failure(ParentLinkErrors.NotFound);
        link.SetProductVisibility(coaching, speedReading, studentUserId, Now);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<ParentReadAuthorization?> AuthorizeReadAsync(Guid parentUserId, Guid studentUserId, PlatformProduct product, CancellationToken ct)
    {
        if (parentUserId == Guid.Empty || studentUserId == Guid.Empty || parentUserId == studentUserId
            || product is not (PlatformProduct.Coaching or PlatformProduct.SpeedReading)) return null;
        var coaching = product == PlatformProduct.Coaching;
        var row = await (from link in db.ParentStudentRelationships.AsNoTracking()
                         where link.ParentUserId == parentUserId && link.StudentUserId == studentUserId
                             && link.Status == ParentStudentRelationshipStatus.Verified
                             && (link.Relationship == ParentRelationship.Mother || link.Relationship == ParentRelationship.Father
                                 || link.Relationship == ParentRelationship.Guardian)
                             && (coaching ? link.CoachingVisible : link.SpeedReadingVisible)
                             && db.Users.Any(u => u.Id == parentUserId && u.IsActive)
                             && db.Users.Any(u => u.Id == studentUserId && u.IsActive)
                             && db.UserProductAccesses.Any(a => a.UserId == studentUserId && a.Product == product && a.IsActive)
                         select new { link.Id, link.Relationship })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : new ParentReadAuthorization(row.Id, row.Relationship.ToString());
    }

    /// <summary>
    /// Verified Mother/Father/Guardian links whose child uses at least one product the child left visible, and whose parent
    /// did not turn the weekly summary off. Filtered, counted and paged in the database: the preference is a jsonb
    /// containment test, which matches the old rule exactly (only an explicit <c>"weeklyProgress": false</c> mutes).
    /// </summary>
    public async Task<ParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber < 1 || pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var muted = db.ParentProfiles.Where(profile => EF.Functions.JsonContains(profile.NotificationPreferences, WeeklySummaryOff))
            .Select(profile => profile.UserId);
        var recipients = from link in db.ParentStudentRelationships.AsNoTracking()
                         join parent in db.Users.AsNoTracking() on link.ParentUserId equals parent.Id
                         join student in db.Users.AsNoTracking() on link.StudentUserId equals student.Id
                         where link.Status == ParentStudentRelationshipStatus.Verified
                             && (link.Relationship == ParentRelationship.Mother || link.Relationship == ParentRelationship.Father
                                 || link.Relationship == ParentRelationship.Guardian)
                             && parent.IsActive && student.IsActive
                             && !muted.Contains(link.ParentUserId)
                         let hasCoaching = db.UserProductAccesses.Any(a => a.UserId == link.StudentUserId && a.Product == PlatformProduct.Coaching && a.IsActive)
                         let hasSpeedReading = db.UserProductAccesses.Any(a => a.UserId == link.StudentUserId && a.Product == PlatformProduct.SpeedReading && a.IsActive)
                         where hasCoaching && link.CoachingVisible || hasSpeedReading && link.SpeedReadingVisible
                         select new
                         {
                             link.Id, link.ParentUserId, parent.Email, ParentFirst = parent.FirstName, ParentLast = parent.LastName,
                             link.StudentUserId, StudentFirst = student.FirstName, StudentLast = student.LastName,
                             link.CoachingVisible, link.SpeedReadingVisible, HasCoaching = hasCoaching, HasSpeedReading = hasSpeedReading
                         };
        var total = await recipients.CountAsync(ct);
        var rows = await recipients.OrderBy(x => x.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var page = rows.Select(x => new ParentSummaryRecipient(x.ParentUserId, x.Email, Name(x.ParentFirst, x.ParentLast), x.StudentUserId,
                Name(x.StudentFirst, x.StudentLast), x.CoachingVisible, x.SpeedReadingVisible, x.HasCoaching, x.HasSpeedReading))
            .ToArray();
        return new ParentSummaryRecipientPage(page, total);
    }

    public async Task<IReadOnlyList<VerifiedParentLink>> GetVerifiedLinksAsync(IReadOnlyCollection<Guid> studentUserIds, Guid? parentUserId, CancellationToken ct)
    {
        var students = studentUserIds.Distinct().ToList();
        if (students.Count > 200) throw new ArgumentOutOfRangeException(nameof(studentUserIds));
        if (students.Count == 0 && parentUserId is null) return [];
        // Audit finding: only the relationships that may read a child's data count as parent links for Coaching.
        var rows = await (from link in db.ParentStudentRelationships.AsNoTracking()
                          join parent in db.Users.AsNoTracking() on link.ParentUserId equals parent.Id
                          join student in db.Users.AsNoTracking() on link.StudentUserId equals student.Id
                          where link.Status == ParentStudentRelationshipStatus.Verified && parent.IsActive && student.IsActive
                              && (link.Relationship == ParentRelationship.Mother || link.Relationship == ParentRelationship.Father
                                  || link.Relationship == ParentRelationship.Guardian)
                              && (students.Contains(link.StudentUserId) || parentUserId != null && link.ParentUserId == parentUserId)
                          orderby link.StudentUserId, link.ParentUserId
                          select new { link.ParentUserId, ParentFirst = parent.FirstName, ParentLast = parent.LastName, link.StudentUserId,
                              StudentFirst = student.FirstName, StudentLast = student.LastName, link.Relationship })
            .ToListAsync(ct);
        return rows.Select(x => new VerifiedParentLink(x.ParentUserId, Name(x.ParentFirst, x.ParentLast), x.StudentUserId,
            Name(x.StudentFirst, x.StudentLast), x.Relationship.ToString())).ToArray();
    }

    /// <summary>Stable per link, step and recipient, so a retried request cannot notify anyone twice.</summary>
    public static Guid EventId(Guid linkId, string change, Guid recipientUserId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"parent-link:{linkId:N}:{change}:{recipientUserId:N}")).AsSpan(0, 16));

    /// <summary>
    /// Queues the notice for the other side in the same SaveChanges as the change itself (Identity uses the
    /// MassTransit EF bus outbox), so a notice is sent exactly when the change is stored.
    /// </summary>
    private async Task NotifyAsync(ParentStudentRelationship link, string change, bool toStudent, DateTime now, CancellationToken ct)
    {
        if (events is null) return;
        var recipientId = toStudent ? link.StudentUserId : link.ParentUserId;
        var otherId = toStudent ? link.ParentUserId : link.StudentUserId;
        var people = await db.Users.AsNoTracking().Where(x => x.Id == recipientId || x.Id == otherId)
            .Select(x => new { x.Id, x.Email, x.FirstName, x.LastName }).ToDictionaryAsync(x => x.Id, ct);
        if (!people.TryGetValue(recipientId, out var recipient) || !people.TryGetValue(otherId, out var other)) return;
        var (product, path) = ("coaching", ParentPath);
        if (toStudent)
        {
            var access = await db.UserProductAccesses.AsNoTracking().Where(a => a.UserId == link.StudentUserId && a.IsActive)
                .Select(a => a.Product).ToListAsync(ct);
            (product, path) = !access.Contains(PlatformProduct.Coaching) && access.Contains(PlatformProduct.SpeedReading)
                ? ("speed-reading", SpeedReadingStudentPath)
                : ("coaching", CoachingStudentPath);
        }
        await events.PublishAsync(new ParentLinkNotificationV1(EventId(link.Id, change, recipientId), link.Id, change, recipientId,
            toStudent ? "Student" : "Parent", string.IsNullOrWhiteSpace(recipient.Email) ? null : recipient.Email, recipient.FirstName,
            Name(other.FirstName, other.LastName), product, path, now), ct);
    }

    private async Task<IReadOnlyList<ParentLinkView>> ViewsAsync(IQueryable<ParentStudentRelationship> links, CancellationToken ct)
    {
        var rows = await (from link in links.AsNoTracking()
                          join parent in db.Users.AsNoTracking() on link.ParentUserId equals parent.Id
                          join student in db.Users.AsNoTracking() on link.StudentUserId equals student.Id
                          orderby link.RequestedAt descending
                          select new
                          {
                              link, ParentFirst = parent.FirstName, ParentLast = parent.LastName, StudentFirst = student.FirstName, StudentLast = student.LastName,
                              HasCoaching = db.UserProductAccesses.Any(a => a.UserId == link.StudentUserId && a.Product == PlatformProduct.Coaching && a.IsActive),
                              HasSpeedReading = db.UserProductAccesses.Any(a => a.UserId == link.StudentUserId && a.Product == PlatformProduct.SpeedReading && a.IsActive)
                          })
            .ToListAsync(ct);
        return rows.Select(x => new ParentLinkView(x.link.Id, x.link.ParentUserId, Name(x.ParentFirst, x.ParentLast), x.link.StudentUserId,
            Name(x.StudentFirst, x.StudentLast), x.link.Relationship.ToString(), x.link.Status.ToString(), x.link.RequestedAt, x.link.VerifiedAt,
            [
                new ParentLinkProductView("Coaching", x.HasCoaching, x.link.CoachingVisible),
                new ParentLinkProductView("SpeedReading", x.HasSpeedReading, x.link.SpeedReadingVisible)
            ])).ToArray();
    }

    private Task<bool> HasActiveRoleAsync(Guid userId, string role, CancellationToken ct) =>
        userId == Guid.Empty ? Task.FromResult(false)
            : db.Users.AnyAsync(x => x.Id == userId && x.IsActive && x.Roles.Any(r => r.Role.Name == role), ct);

    private static string? Normalize(string? code)
    {
        var value = new string((code ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return value.Length == 8 && value.All(Alphabet.Contains) ? value : null;
    }

    private static string Hash(string normalizedCode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("parent-link:" + normalizedCode)));

    private static string Name(string first, string last) => $"{first} {last}".Trim();

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
}
