using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public sealed record TeacherEmailInvitationRequest(Guid Id, string? Email);
public sealed record AcceptTeacherInvitationRequest(string? DisplayName);

/// <summary>
/// Teachers invited by e-mail (maxkampus.com, 19.11.2026). The owner writes an address; the teacher gets a link to
/// maxkampus.com/davet/{code}, opens an account there (or uses one with the same address) and accepts.
/// </summary>
public static class TeacherInvitationEndpoints
{
    public static void MapTeacherInvitationEndpoints(this RouteGroupBuilder api, WebApplication app)
    {
        api.MapPost("/workspaces/{workspaceId:guid}/teacher-email-invitations", async (Guid workspaceId, TeacherEmailInvitationRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publisher, CourseEmailOptions emailOptions, CancellationToken ct) =>
        {
            var workspace = await OwnedAsync(db, workspaceId, user, ct);
            if (workspace is null) return Results.NotFound();
            string? email;
            try { email = WorkspaceEmailSettings.NormalizeEmail(request.Email); }
            catch (ArgumentException) { email = null; }
            if (email is null || request.Id == Guid.Empty) return Results.BadRequest(new { code = "Invitation.InvalidEmail" });

            var now = DateTimeOffset.UtcNow;
            var existing = await db.TeacherEmailInvitations.SingleOrDefaultAsync(item => item.Id == request.Id, ct);
            if (existing is not null && (existing.WorkspaceId != workspaceId || existing.Email != email))
                return Results.Conflict(new { code = "Invitation.RequestConflict" });
            // A second invitation to the same address renews the open one: new link, new expiry.
            existing ??= await db.TeacherEmailInvitations.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.Email == email
                && item.Status == InvitationStatus.Pending, ct);
            if (existing is { Status: InvitationStatus.Accepted }) return Results.Conflict(new { code = "Invitation.AlreadyAccepted" });
            string code;
            if (existing is null)
            {
                (existing, code) = TeacherEmailInvitation.Create(request.Id, workspaceId, email, UserId(user), now);
                db.TeacherEmailInvitations.Add(existing);
            }
            else code = existing.Renew(now);
            await publisher.Publish(InvitationEmail(emailOptions, workspace.Name, email, code), ct);
            db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, UserId(user), existing.Id, "TeacherInvitation.Sent"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(View(existing, now));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/teacher-email-invitations", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            var rows = await db.TeacherEmailInvitations.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(ct);
            return Results.Ok(rows.Select(item => View(item, now)));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/teacher-email-invitations/{invitationId:guid}/cancel", async (Guid workspaceId, Guid invitationId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var invitation = await db.TeacherEmailInvitations.SingleOrDefaultAsync(item => item.Id == invitationId && item.WorkspaceId == workspaceId, ct);
            if (invitation is null) return Results.NotFound();
            if (invitation.Status == InvitationStatus.Cancelled) return Results.NoContent();
            if (invitation.Status != InvitationStatus.Pending) return Results.Conflict(new { code = "Invitation.NotPending" });
            invitation.Cancel();
            db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, UserId(user), invitationId, "TeacherInvitation.Cancelled"));
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // The teacher, signed in to maxkampus.com with the invited address, joins the institution.
        api.MapPost("/me/teacher-invitations/{code}/accept", async (string code, AcceptTeacherInvitationRequest request, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!TeacherEmailInvitation.IsWellFormedCode(code)) return Results.NotFound();
            var displayName = request.DisplayName?.Trim();
            if (string.IsNullOrEmpty(displayName) || displayName.Length > 200) return Results.BadRequest(new { code = "Profile.InvalidName" });
            var hash = TeacherEmailInvitation.HashCode(code);
            var invitation = await db.TeacherEmailInvitations.SingleOrDefaultAsync(item => item.CodeHash == hash, ct);
            if (invitation is null) return Results.NotFound();
            var userId = UserId(user);
            var now = DateTimeOffset.UtcNow;
            if (invitation.Status == InvitationStatus.Accepted)
                return invitation.AcceptedUserId == userId ? Results.Ok(new { invitation.WorkspaceId }) : Results.Conflict(new { code = "Invitation.NotAvailable" });
            if (!invitation.IsOpen(now)) return Results.Conflict(new { code = "Invitation.NotAvailable" });
            var email = user.FindFirstValue("email")?.Trim().ToLowerInvariant() ?? user.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
            if (email != invitation.Email) return Results.Conflict(new { code = "Invitation.OtherEmail" });
            if (await db.Workspaces.AnyAsync(item => item.Id == invitation.WorkspaceId && item.OwnerUserId == userId, ct))
                return Results.Conflict(new { code = "Invitation.InvalidTarget" });

            var member = await db.Memberships.FindAsync([invitation.WorkspaceId, userId], ct);
            if (member?.IsActive == true && member.Role != WorkspaceRole.Teacher) return Results.Conflict(new { code = "Membership.RoleConflict" });
            if (member is null) db.Memberships.Add(WorkspaceMembership.Create(invitation.WorkspaceId, userId, WorkspaceRole.Teacher));
            else if (!member.IsActive) member.Activate(WorkspaceRole.Teacher);
            if (await db.UserProfiles.FindAsync([userId], ct) is null) db.UserProfiles.Add(CourseUserProfile.Create(userId, displayName));
            invitation.Accept(userId, now);
            db.AuditEntries.Add(WorkspaceAuditEntry.Create(invitation.WorkspaceId, userId, invitation.Id, "TeacherInvitation.Accepted"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { invitation.WorkspaceId });
        });

        // What the invitation page shows before sign-in: whose invitation, to which address, until when.
        app.MapGet("/api/course-management/public/teacher-invitations/{code}", async (string code, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!TeacherEmailInvitation.IsWellFormedCode(code)) return Results.NotFound();
            var hash = TeacherEmailInvitation.HashCode(code);
            var row = await (from invitation in db.TeacherEmailInvitations.AsNoTracking()
                join workspace in db.Workspaces.AsNoTracking() on invitation.WorkspaceId equals workspace.Id
                where invitation.CodeHash == hash
                select new { invitation.Email, invitation.Status, invitation.ExpiresAt, workspace.Name }).SingleOrDefaultAsync(ct);
            if (row is null) return Results.NotFound();
            var status = row.Status == InvitationStatus.Pending && row.ExpiresAt <= DateTimeOffset.UtcNow ? "Expired" : row.Status.ToString();
            return Results.Ok(new { workspaceName = row.Name, email = row.Email, status, expiresAt = row.ExpiresAt });
        }).AllowAnonymous();
    }

    public static CourseManagementOwnerEmailRequestedV1 InvitationEmail(CourseEmailOptions options, string workspaceName, string email, string code) => new(
        Guid.NewGuid(), email, $"{workspaceName} sizi maxkampüs'e davet etti", "Öğretmen davetiniz var",
        [
            $"{workspaceName}, derslerinizi, takviminizi ve yoklamanızı maxkampüs'te birlikte yönetmek için sizi öğretmen olarak davet etti.",
            "Daveti açın; hesabınız yoksa bu e-posta adresiyle birkaç adımda açabilirsiniz. Davet 14 gün geçerlidir.",
            "Bu daveti beklemiyorsanız e-postayı yok sayabilirsiniz."
        ],
        $"{options.PublicBaseUrl.TrimEnd('/')}/davet/{code}", "Daveti aç");

    private static object View(TeacherEmailInvitation invitation, DateTimeOffset now) => new
    {
        invitation.Id,
        invitation.Email,
        status = invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= now ? "Expired" : invitation.Status.ToString(),
        invitation.CreatedAt,
        invitation.ExpiresAt,
        invitation.AcceptedAt
    };

    private static Task<Workspace?> OwnedAsync(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user, CancellationToken ct) =>
        db.Workspaces.SingleOrDefaultAsync(workspace => workspace.Id == workspaceId && workspace.OwnerUserId == UserId(user), ct);

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
}
