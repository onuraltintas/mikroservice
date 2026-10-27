using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Results;
using Identity.Application.ParentLinks;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class ParentLinkService(IdentityDbContext db, TimeProvider clock) : IParentLinkService
{
    // No 0/O, 1/I/L: the code is read aloud or typed from a phone screen.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

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
        link.Verify(ParentStudentVerificationMethod.InvitationAcceptance, studentUserId, Now);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RevokeAsync(Guid userId, Guid linkId, CancellationToken ct)
    {
        var link = await db.ParentStudentRelationships.SingleOrDefaultAsync(x => x.Id == linkId
            && (x.StudentUserId == userId || x.ParentUserId == userId) && x.Status != ParentStudentRelationshipStatus.Revoked, ct);
        if (link is null) return Result.Failure(ParentLinkErrors.NotFound);
        link.Revoke(userId, Now, link.StudentUserId == userId ? "Öğrenci veli bağlantısını kaldırdı." : "Veli bağlantıyı kaldırdı.");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<ParentSummaryRecipientPage> GetWeeklySummaryRecipientsAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber < 1 || pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var verified = from link in db.ParentStudentRelationships.AsNoTracking()
                       join parent in db.Users.AsNoTracking() on link.ParentUserId equals parent.Id
                       join student in db.Users.AsNoTracking() on link.StudentUserId equals student.Id
                       where link.Status == ParentStudentRelationshipStatus.Verified
                           && (link.Relationship == ParentRelationship.Mother || link.Relationship == ParentRelationship.Father
                               || link.Relationship == ParentRelationship.Guardian)
                           && parent.IsActive && student.IsActive
                       orderby link.Id
                       select new
                       {
                           link.ParentUserId, parent.Email, ParentFirst = parent.FirstName, ParentLast = parent.LastName,
                           link.StudentUserId, StudentFirst = student.FirstName, StudentLast = student.LastName,
                           Preferences = db.ParentProfiles.Where(x => x.UserId == link.ParentUserId).Select(x => x.NotificationPreferences).FirstOrDefault()
                       };
        var rows = (await verified.ToListAsync(ct)).Where(x => WantsWeeklySummary(x.Preferences)).ToArray();
        var page = rows.Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new ParentSummaryRecipient(x.ParentUserId, x.Email, Name(x.ParentFirst, x.ParentLast), x.StudentUserId, Name(x.StudentFirst, x.StudentLast)))
            .ToArray();
        return new ParentSummaryRecipientPage(page, rows.Length);
    }

    private async Task<IReadOnlyList<ParentLinkView>> ViewsAsync(IQueryable<ParentStudentRelationship> links, CancellationToken ct)
    {
        var rows = await (from link in links.AsNoTracking()
                          join parent in db.Users.AsNoTracking() on link.ParentUserId equals parent.Id
                          join student in db.Users.AsNoTracking() on link.StudentUserId equals student.Id
                          orderby link.RequestedAt descending
                          select new { link, ParentFirst = parent.FirstName, ParentLast = parent.LastName, StudentFirst = student.FirstName, StudentLast = student.LastName })
            .ToListAsync(ct);
        return rows.Select(x => new ParentLinkView(x.link.Id, x.link.ParentUserId, Name(x.ParentFirst, x.ParentLast), x.link.StudentUserId,
            Name(x.StudentFirst, x.StudentLast), x.link.Relationship.ToString(), x.link.Status.ToString(), x.link.RequestedAt, x.link.VerifiedAt)).ToArray();
    }

    private Task<bool> HasActiveRoleAsync(Guid userId, string role, CancellationToken ct) =>
        userId == Guid.Empty ? Task.FromResult(false)
            : db.Users.AnyAsync(x => x.Id == userId && x.IsActive && x.Roles.Any(r => r.Role.Name == role), ct);

    private static bool WantsWeeklySummary(string? preferences)
    {
        if (string.IsNullOrWhiteSpace(preferences)) return true;
        try
        {
            using var json = JsonDocument.Parse(preferences);
            return !json.RootElement.TryGetProperty("weeklyProgress", out var value) || value.ValueKind != JsonValueKind.False;
        }
        catch (JsonException) { return true; }
    }

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
