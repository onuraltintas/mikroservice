using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

/// <param name="Action">Invite, AlreadyMember, PendingInvitation, Link, AlreadyLinked or Error.</param>
public sealed record InstitutionBulkRow(int Line, string Email, string? TeacherEmail, string Action, string? Error);
public sealed record InstitutionBulkPreview(string Kind, string Fingerprint, IReadOnlyList<InstitutionBulkRow> Rows, int ToApply, bool HasErrors);
public sealed record InstitutionBulkResult(int Applied, IReadOnlyList<InstitutionBulkRow> Rows);
public sealed record InstitutionBulkRequest(string? Csv, string? Fingerprint, string? XlsxBase64 = null);

/// <summary>
/// Roadmap O13: CSV (or Excel) bulk student invitations and teacher links for an institution. As with exam results
/// (O4) a file is first previewed line by line; only an error-free file with the same fingerprint is applied.
/// </summary>
public sealed class CoachingInstitutionBulkService(CoachingDbContext db, ICoachingAccountDirectory accounts,
    CoachingProfileStore profiles, CoachingInvitationStore invitations)
{
    public const int MaxRows = 300;
    private static readonly string[] EmailHeaders = ["eposta", "e-posta", "email", "ogrenci_eposta", "öğrenci_eposta", "student_email"];
    private static readonly string[] TeacherHeaders = ["ogretmen_eposta", "öğretmen_eposta", "teacher_email"];

    public Task<InstitutionBulkPreview> PreviewInvitationsAsync(Guid actor, Guid institution, string csv, CancellationToken ct) =>
        PreviewAsync(actor, institution, "Invitations", csv, ct);

    public Task<InstitutionBulkPreview> PreviewTeacherLinksAsync(Guid actor, Guid institution, string csv, CancellationToken ct) =>
        PreviewAsync(actor, institution, "TeacherLinks", csv, ct);

    public async Task<InstitutionBulkResult> ApplyInvitationsAsync(Guid actor, Guid institution, string csv, string fingerprint, CancellationToken ct)
    {
        var preview = await Checked(actor, institution, "Invitations", csv, fingerprint, ct);
        var applied = 0; var teachers = await TeacherIdsAsync(preview.Rows.Select(x => x.TeacherEmail), ct);
        foreach (var row in preview.Rows.Where(x => x.Action == "Invite"))
        {
            await invitations.SendInstitutionAsync(actor, institution, row.Email, "Student",
                row.TeacherEmail is null ? null : teachers[row.TeacherEmail], null, ct);
            applied++;
        }
        return new(applied, preview.Rows);
    }

    public async Task<InstitutionBulkResult> ApplyTeacherLinksAsync(Guid actor, Guid institution, string csv, string fingerprint, CancellationToken ct)
    {
        var preview = await Checked(actor, institution, "TeacherLinks", csv, fingerprint, ct);
        var applied = 0;
        foreach (var row in preview.Rows.Where(x => x.Action == "Link"))
        {
            var student = (await accounts.FindByEmailAsync(row.Email, ct))!.UserId;
            var teacher = (await accounts.FindByEmailAsync(row.TeacherEmail!, ct))!.UserId;
            await profiles.AddInstitutionTeacherLinkAsync(actor, institution, student, teacher, ct);
            applied++;
        }
        return new(applied, preview.Rows);
    }

    private async Task<InstitutionBulkPreview> Checked(Guid actor, Guid institution, string kind, string csv, string fingerprint, CancellationToken ct)
    {
        var preview = await PreviewAsync(actor, institution, kind, csv, ct);
        if (!string.Equals(preview.Fingerprint, fingerprint, StringComparison.Ordinal))
            throw new BusinessRuleException("Bulk.Changed", "Dosya önizlemeden sonra değişti. Yeniden önizleyin.");
        if (preview.HasErrors) throw new ArgumentException("Hatalı satırları düzeltip dosyayı yeniden önizleyin.");
        return preview;
    }

    private async Task<InstitutionBulkPreview> PreviewAsync(Guid actor, Guid institution, string kind, string csv, CancellationToken ct)
    {
        await profiles.RequireInstitutionAdminAsync(actor, institution, ct);
        var lines = Parse(csv, kind == "TeacherLinks");
        var rows = new List<InstitutionBulkRow>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var members = (await db.CoachingInstitutionMemberships.AsNoTracking().Where(x => x.InstitutionId == institution && x.IsActive)
            .Select(x => new { x.UserId, x.Role }).ToListAsync(ct)).ToLookup(x => x.Role, x => x.UserId);
        var studentMembers = members[CoachingMemberRole.Student].ToHashSet(); var teacherMembers = members[CoachingMemberRole.Teacher].ToHashSet();
        var now = DateTime.UtcNow;
        foreach (var (line, email, teacherEmail) in lines)
        {
            InstitutionBulkRow Row(string action, string? error = null) => new(line, email, teacherEmail, action, error);
            if (!ValidEmail(email)) { rows.Add(Row("Error", "Geçerli bir e-posta adresi yazın.")); continue; }
            if (!seen.Add(email)) { rows.Add(Row("Error", "Bu e-posta dosyada birden fazla kez var.")); continue; }
            Guid? teacher = null;
            if (teacherEmail is not null)
            {
                var account = ValidEmail(teacherEmail) ? await accounts.FindByEmailAsync(teacherEmail, ct) : null;
                if (account is null || !teacherMembers.Contains(account.UserId)) { rows.Add(Row("Error", "Öğretmen bu kurumda bulunamadı.")); continue; }
                teacher = account.UserId;
            }
            var person = await accounts.FindByEmailAsync(email, ct);
            if (kind == "Invitations")
            {
                if (person is not null && studentMembers.Contains(person.UserId)) { rows.Add(Row("AlreadyMember")); continue; }
                var pending = await db.CoachingInvitations.AsNoTracking().AnyAsync(x => x.RecipientEmail == email.ToLowerInvariant()
                    && x.InstitutionId == institution && x.Role == "Student" && x.Status == CoachingInvitationStatus.Pending && x.ExpiresAt > now, ct);
                rows.Add(Row(pending ? "PendingInvitation" : "Invite"));
                continue;
            }
            if (teacher is null) { rows.Add(Row("Error", "Öğretmen e-postası gerekli.")); continue; }
            if (person is null || !studentMembers.Contains(person.UserId)) { rows.Add(Row("Error", "Öğrenci bu kurumun üyesi değil.")); continue; }
            var linked = await db.CoachingTeacherStudentAssignments.AsNoTracking().AnyAsync(x => x.StudentUserId == person.UserId
                && x.TeacherUserId == teacher && x.InstitutionId == institution && x.IsActive, ct);
            rows.Add(Row(linked ? "AlreadyLinked" : "Link"));
        }
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(kind + "|" + institution.ToString("N") + "|"
            + string.Join("\n", lines.Select(x => $"{x.Email.ToLowerInvariant()};{x.TeacherEmail?.ToLowerInvariant()}")))));
        return new(kind, fingerprint, rows, rows.Count(x => x.Action is "Invite" or "Link"), rows.Any(x => x.Action == "Error"));
    }

    private async Task<Dictionary<string, Guid>> TeacherIdsAsync(IEnumerable<string?> emails, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in emails.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            result[email] = (await accounts.FindByEmailAsync(email, ct))!.UserId;
        return result;
    }

    private static bool ValidEmail(string value) =>
        value.Length <= 254 && MailAddress.TryCreate(value, out var address) && address.Address.Equals(value, StringComparison.OrdinalIgnoreCase) && value.Contains('.');

    /// <summary>Header row required; ';' or ',' separated; blank lines skipped; at most 300 rows.</summary>
    public static IReadOnlyList<(int Line, string Email, string? TeacherEmail)> Parse(string? csv, bool teacherRequired)
    {
        var text = (csv ?? string.Empty).TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n');
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0])) throw new ArgumentException("Dosya boş. İlk satır başlık olmalıdır.");
        var separator = lines[0].Count(c => c == ';') >= lines[0].Count(c => c == ',') ? ';' : ',';
        string[] Cells(string line) => line.Split(separator).Select(x => x.Trim().Trim('"').Trim()).ToArray();
        var header = Cells(lines[0]).Select(x => x.ToLowerInvariant()).ToArray();
        var emailColumn = Array.FindIndex(header, x => EmailHeaders.Contains(x));
        var teacherColumn = Array.FindIndex(header, x => TeacherHeaders.Contains(x));
        if (emailColumn < 0) throw new ArgumentException("Başlıkta e-posta sütunu yok (ör. eposta veya ogrenci_eposta).");
        if (teacherRequired && teacherColumn < 0) throw new ArgumentException("Başlıkta ogretmen_eposta sütunu yok.");
        var rows = new List<(int, string, string?)>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = Cells(lines[i]);
            var email = emailColumn < cells.Length ? cells[emailColumn] : string.Empty;
            var teacher = teacherColumn >= 0 && teacherColumn < cells.Length && cells[teacherColumn].Length > 0 ? cells[teacherColumn] : null;
            rows.Add((i + 1, email, teacher));
            if (rows.Count > MaxRows) throw new ArgumentException($"Bir dosyada en fazla {MaxRows} satır olabilir.");
        }
        if (rows.Count == 0) throw new ArgumentException("Dosyada başlıktan sonra satır yok.");
        return rows;
    }
}
