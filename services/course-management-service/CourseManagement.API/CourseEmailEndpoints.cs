using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

/// <summary>The institution's e-mail settings, sending record and per-student switch; the public unsubscribe page.</summary>
public static class CourseEmailEndpoints
{
    public const int TestEmailsPerDay = 10;

    public static void MapCourseEmailEndpoints(this RouteGroupBuilder api, WebApplication app)
    {
        api.MapGet("/workspaces/{workspaceId:guid}/email-settings", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            var settings = await db.EmailSettings.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId, ct)
                ?? WorkspaceEmailSettings.CreateDefault(workspaceId, DateTimeOffset.UtcNow);
            return Results.Ok(View(settings));
        });

        api.MapPut("/workspaces/{workspaceId:guid}/email-settings", async (Guid workspaceId, UpdateEmailSettingsRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            if (request.Rules is null || request.Rules.Length > Enum.GetValues<CourseEmailKind>().Length)
                return Results.BadRequest(new { code = "EmailSettings.InvalidInput" });
            var now = DateTimeOffset.UtcNow;
            var settings = await db.EmailSettings.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId, ct);
            var created = settings is null;
            settings ??= WorkspaceEmailSettings.CreateDefault(workspaceId, now);
            try
            {
                settings.Update(request.Rules.Select(rule => new CourseEmailRule(rule.Kind, rule.Enabled, rule.Audience, rule.Note)).ToArray(),
                    request.InstallmentDaysBefore, request.InstallmentOnDueDay, request.OverdueDaysAfter, request.SendHour,
                    request.ReplyToEmail, request.InformedConsentConfirmed, UserId(user), now);
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { code = "EmailSettings.ConsentRequired" });
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { code = "EmailSettings.InvalidInput" });
            }
            if (created) db.EmailSettings.Add(settings);
            AssignmentEndpoints.AddAudit(db, workspaceId, UserId(user), workspaceId, "EmailSettings.Updated");
            await db.SaveChangesAsync(ct);
            return Results.Ok(View(settings));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/email-settings/preview/{kind}", async (Guid workspaceId, CourseEmailKind kind,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!Enum.IsDefined(kind)) return Results.NotFound();
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            var name = await db.Workspaces.AsNoTracking().Where(item => item.Id == workspaceId).Select(item => item.Name).SingleAsync(ct);
            var note = (await db.EmailSettings.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId, ct))
                ?.Rule(kind).Note;
            var sample = CourseEmailContent.Sample(kind);
            return Results.Ok(new EmailPreviewView(name, sample.Subject, sample.Title, sample.Paragraphs, note));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/email-settings/test", async (Guid workspaceId, SendTestEmailRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publish, CourseEmailOptions options, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            string? address;
            try { address = WorkspaceEmailSettings.NormalizeEmail(request.Email); }
            catch (ArgumentException) { address = null; }
            if (address is null || !Enum.IsDefined(request.Kind)) return Results.BadRequest(new { code = "EmailTest.InvalidInput" });
            var dayStart = DateTimeOffset.UtcNow.AddDays(-1);
            if (await db.AuditEntries.CountAsync(entry => entry.WorkspaceId == workspaceId && entry.Action == "Email.TestSent"
                    && entry.OccurredAt >= dayStart, ct) >= TestEmailsPerDay)
                return Results.Json(new { code = "EmailTest.DailyLimit" }, statusCode: StatusCodes.Status429TooManyRequests);
            var workspace = await db.Workspaces.AsNoTracking().SingleAsync(item => item.Id == workspaceId, ct);
            var settings = await db.EmailSettings.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId, ct);
            var sample = CourseEmailContent.Sample(request.Kind);
            var id = Guid.NewGuid();
            await publish.Publish(new CourseManagementEmailRequestedV1(id, workspaceId, workspace.Name, settings?.ReplyToEmail,
                    address, "Deneme: " + sample.Subject, sample.Title, sample.Paragraphs, settings?.Rule(request.Kind).Note,
                    CourseEmailLinks.UnsubscribeUrl(options, workspaceId, address)),
                context => context.MessageId = id, ct);
            AssignmentEndpoints.AddAudit(db, workspaceId, UserId(user), id, "Email.TestSent");
            await db.SaveChangesAsync(ct);
            return Results.Accepted();
        });

        api.MapGet("/workspaces/{workspaceId:guid}/emails", async (Guid workspaceId, Guid? studentId, int? skip, int? take,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            if (skip is < 0 || take is < 1 or > 200) return Results.BadRequest(new { code = "Email.InvalidPage" });
            var query = db.Emails.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.Status != CourseEmailStatus.Pending);
            if (studentId is Guid id) query = query.Where(item => item.StudentId == id);
            var rows = await (from email in query
                join student in db.Students.AsNoTracking() on email.StudentId equals student.Id
                orderby email.ProcessedAt descending, email.Id
                select new SentEmailView(email.Id, email.Kind.ToString(), email.StudentId, student.DisplayName,
                    email.Status.ToString(), email.Subject, email.Recipients, email.RecipientCount, email.SkipReason,
                    email.ProcessedAt)).Skip(skip ?? 0).Take(take ?? 50).ToListAsync(ct);
            return Results.Ok(rows);
        });

        api.MapPut("/workspaces/{workspaceId:guid}/students/{studentId:guid}/email-mute", async (Guid workspaceId, Guid studentId,
            SetEmailMuteRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            var student = await db.Students.SingleOrDefaultAsync(item => item.Id == studentId && item.WorkspaceId == workspaceId, ct);
            if (student is null) return Results.NotFound();
            if (student.EmailsMuted != request.Muted)
            {
                student.SetEmailsMuted(request.Muted);
                AssignmentEndpoints.AddAudit(db, workspaceId, UserId(user), studentId, request.Muted ? "Student.EmailsMuted" : "Student.EmailsUnmuted");
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new { studentId, emailsMuted = student.EmailsMuted });
        });

        // Opened from the link in an e-mail: no account. A GET only shows the button (mail scanners follow links).
        var publicEmail = app.MapGroup("/api/course-management/public/email").AllowAnonymous();
        publicEmail.MapGet("/unsubscribe", async (string? w, string? e, string? t, CourseManagementDbContext db,
            CourseEmailOptions options, CancellationToken ct) =>
        {
            var target = await ResolveAsync(db, options, w, e, t, ct);
            if (target is null) return Page("Bağlantı geçersiz", "Bu bağlantı geçerli değil. E-postadaki bağlantıyı yeniden açın.");
            var (workspace, email) = target.Value;
            if (await db.EmailOptOuts.AnyAsync(item => item.WorkspaceId == workspace.Id && item.Email == email, ct))
                return Page("E-postalardan çıktınız", $"{workspace.Name} bu adrese artık e-posta göndermiyor.");
            return Page("E-postalardan çık", $"{email} adresi {workspace.Name} e-postalarını artık almayacak.",
                $"<form method=\"post\"><input type=\"hidden\" name=\"w\" value=\"{Html(w!)}\"><input type=\"hidden\" name=\"e\" value=\"{Html(e!)}\">"
                + $"<input type=\"hidden\" name=\"t\" value=\"{Html(t!)}\"><button type=\"submit\">E-postalardan çık</button></form>");
        });
        publicEmail.MapPost("/unsubscribe", async (HttpRequest request, CourseManagementDbContext db, CourseEmailOptions options,
            CancellationToken ct) =>
        {
            // The page's button posts the fields; a mail program's one-click unsubscribe (RFC 8058) posts to the link itself.
            var form = request.HasFormContentType ? await request.ReadFormAsync(ct) : null;
            string? Field(string name) => form is not null && form.ContainsKey(name) ? form[name].ToString() : request.Query[name].ToString();
            var target = await ResolveAsync(db, options, Field("w"), Field("e"), Field("t"), ct);
            if (target is null) return Page("Bağlantı geçersiz", "Bu bağlantı geçerli değil. E-postadaki bağlantıyı yeniden açın.");
            var (workspace, email) = target.Value;
            if (!await db.EmailOptOuts.AnyAsync(item => item.WorkspaceId == workspace.Id && item.Email == email, ct))
            {
                db.EmailOptOuts.Add(CourseEmailOptOut.Create(workspace.Id, email, DateTimeOffset.UtcNow));
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException) { /* the same address clicked twice at once */ }
            }
            return Page("E-postalardan çıktınız", $"{workspace.Name} bu adrese artık e-posta göndermiyor. "
                + "Yeniden almak isterseniz kurumla iletişime geçin.");
        }).DisableAntiforgery();
    }

    private static async Task<(Workspace Workspace, string Email)?> ResolveAsync(CourseManagementDbContext db, CourseEmailOptions options,
        string? w, string? e, string? t, CancellationToken ct)
    {
        if (!Guid.TryParseExact(w, "N", out var workspaceId)) return null;
        var decoded = CourseEmailLinks.DecodeEmail(e);
        string? email;
        try { email = WorkspaceEmailSettings.NormalizeEmail(decoded); }
        catch (ArgumentException) { return null; }
        if (email is null || !CourseEmailLinks.IsValid(options, workspaceId, email, t)) return null;
        var workspace = await db.Workspaces.AsNoTracking().SingleOrDefaultAsync(item => item.Id == workspaceId, ct);
        return workspace is null ? null : (workspace, email);
    }

    private static IResult Page(string title, string message, string? form = null) => Results.Content($$"""
        <!DOCTYPE html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex"><title>{{Html(title)}}</title>
        <style>body{font-family:system-ui,sans-serif;background:#F4F5F7;color:#1C2430;margin:0;padding:48px 16px}
        main{max-width:480px;margin:0 auto;background:#fff;border:1px solid #D6DAE0;border-radius:10px;padding:28px}
        h1{font-size:22px;margin:0 0 12px}p{line-height:1.6}
        button{min-height:44px;padding:0 20px;border:0;border-radius:8px;background:#1C2430;color:#fff;font-size:16px;cursor:pointer}
        button:focus-visible{outline:3px solid #B7791F;outline-offset:2px}</style></head>
        <body><main><h1>{{Html(title)}}</h1><p>{{Html(message)}}</p>{{form}}</main></body></html>
        """, "text/html; charset=utf-8");

    // Turkish letters stay readable in the page source; markup characters are still encoded.
    private static readonly System.Text.Encodings.Web.HtmlEncoder PageEncoder =
        System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All);

    private static string Html(string value) => PageEncoder.Encode(value);

    private static Task<bool> IsOwner(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = UserId(user);
        return db.Workspaces.AnyAsync(item => item.Id == workspaceId && item.OwnerUserId == userId, ct);
    }

    private static Guid UserId(ClaimsPrincipal user) => AssignmentEndpoints.UserId(user);

    private static EmailSettingsView View(WorkspaceEmailSettings settings) => new(
        settings.Rules.Select(rule => new EmailRuleView(rule.Kind.ToString(), (int)rule.Kind, rule.Enabled, (int)rule.Audience, rule.Note))
            .ToArray(),
        settings.InstallmentDaysBefore, settings.InstallmentOnDueDay, settings.OverdueDaysAfter, settings.SendHour,
        settings.ReplyToEmail, settings.InformedConsentAt);
}

public sealed record EmailRuleRequest(CourseEmailKind Kind, bool Enabled, CourseEmailAudience Audience, string? Note);
public sealed record UpdateEmailSettingsRequest(EmailRuleRequest[]? Rules, int InstallmentDaysBefore, bool InstallmentOnDueDay,
    int OverdueDaysAfter, int SendHour, string? ReplyToEmail, bool InformedConsentConfirmed);
public sealed record EmailRuleView(string Kind, int KindValue, bool Enabled, int Audience, string? Note);
public sealed record EmailSettingsView(EmailRuleView[] Rules, int InstallmentDaysBefore, bool InstallmentOnDueDay,
    int OverdueDaysAfter, int SendHour, string? ReplyToEmail, DateTimeOffset? InformedConsentAt);
public sealed record EmailPreviewView(string InstitutionName, string Subject, string Title, IReadOnlyList<string> Paragraphs, string? Note);
public sealed record SendTestEmailRequest(CourseEmailKind Kind, string? Email);
public sealed record SentEmailView(Guid Id, string Kind, Guid StudentId, string StudentName, string Status, string? Subject,
    string? Recipients, int RecipientCount, string? SkipReason, DateTimeOffset? ProcessedAt);
public sealed record SetEmailMuteRequest(bool Muted);
