using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this WebApplication app, RouteGroupBuilder api)
    {
        api.MapGet("/workspaces/{workspaceId:guid}", async (Guid workspaceId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == UserId(user) && item.IsActive, ct);
            if (member is null) return Results.NotFound();
            var workspace = await db.Workspaces.AsNoTracking().SingleAsync(item => item.Id == workspaceId, ct);
            return Results.Ok(new { workspace.Id, workspace.Name, type = workspace.Type.ToString(),
                institutionCategory = workspace.InstitutionCategory?.ToString(), role = member.Role.ToString(),
                isReadOnly = workspace.IsErasureReadOnly });
        });
        api.MapGet("/me/memberships", async (ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var memberships = await (from membership in db.Memberships.AsNoTracking()
                join workspace in db.Workspaces.AsNoTracking() on membership.WorkspaceId equals workspace.Id
                join subscriptionJoin in db.WorkspaceSubscriptions.AsNoTracking()
                    on workspace.Id equals subscriptionJoin.WorkspaceId into subscriptions
                from subscription in subscriptions.DefaultIfEmpty()
                where membership.UserId == UserId(user) && membership.IsActive
                orderby workspace.Name
                select new
                {
                    workspace.Id,
                    workspace.Name,
                    type = workspace.Type.ToString(),
                    institutionCategory = workspace.InstitutionCategory.HasValue ? workspace.InstitutionCategory.Value.ToString() : null,
                    role = membership.Role.ToString(),
                    isReadOnly = workspace.IsErasureReadOnly || (subscription != null && (subscription.AccessStatus != WorkspaceAccessStatus.Active
                        || subscription.StartsAt > now || subscription.EndsAt <= now))
                }).Take(100).ToListAsync(ct);
            return Results.Ok(memberships);
        });
        api.MapGet("/me/subscriptions", async (ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            var rows = await (from workspace in db.Workspaces.AsNoTracking()
                where workspace.OwnerUserId == userId
                join plan in db.WorkspaceSubscriptions.AsNoTracking() on workspace.Id equals plan.WorkspaceId into plans
                from subscription in plans.DefaultIfEmpty()
                orderby workspace.Name, workspace.Id
                select new
                {
                    workspaceId = workspace.Id,
                    workspaceName = workspace.Name,
                    workspaceType = workspace.Type.ToString(),
                    activeStudentCount = db.Students.Count(student => student.WorkspaceId == workspace.Id && student.IsActive),
                    planName = subscription == null ? null : subscription.PlanName,
                    activeStudentLimit = subscription == null ? (int?)null : subscription.ActiveStudentLimit,
                    startsAt = subscription == null ? (DateTimeOffset?)null : subscription.StartsAt,
                    endsAt = subscription == null ? (DateTimeOffset?)null : subscription.EndsAt,
                    accessStatus = subscription == null ? (WorkspaceAccessStatus?)null : subscription.AccessStatus
                }).Take(100).ToListAsync(ct);

            return Results.Ok(rows.Select(row => new
            {
                row.workspaceId,
                row.workspaceName,
                row.workspaceType,
                row.activeStudentCount,
                subscription = row.planName is null ? null : new
                {
                    planName = row.planName,
                    activeStudentLimit = row.activeStudentLimit,
                    startsAt = row.startsAt,
                    endsAt = row.endsAt,
                    accessStatus = row.accessStatus!.Value.ToString()
                }
            }));
        });

        api.MapPost("/workspaces", async (CreateWorkspaceRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200 || !Enum.IsDefined(request.Type))
                return Results.BadRequest(new { code = "Workspace.InvalidInput" });
            if (request.Type == WorkspaceType.IndependentTeacher
                ? !(user.IsInRole("CourseTeacher") || user.IsInRole("Teacher"))
                : !(user.IsInRole("CourseInstitutionOwner") || user.IsInRole("InstitutionOwner") || user.IsInRole("InstitutionAdmin")))
                return Results.Forbid();
            var existing = await db.Workspaces.FindAsync([request.Id], ct);
            if (existing is not null)
                return existing.OwnerUserId == UserId(user) && existing.Name == request.Name.Trim() && existing.Type == request.Type
                    ? Results.Ok(WorkspaceView(existing)) : Results.Conflict(new { code = "Workspace.RequestConflict" });
            var workspace = Workspace.Create(request.Id, UserId(user), request.Name, request.Type);
            db.Workspaces.Add(workspace);
            db.Memberships.Add(WorkspaceMembership.Create(workspace.Id, UserId(user), WorkspaceRole.Owner));
            Audit(db, workspace.Id, UserId(user), workspace.Id, "Workspace.Created");
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspace.Id}", WorkspaceView(workspace));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/members", async (Guid workspaceId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await Owns(db, workspaceId, user, ct)) return Results.NotFound();
            return Results.Ok(await db.Memberships.AsNoTracking().Where(member => member.WorkspaceId == workspaceId)
                .OrderBy(member => member.UserId).Take(100)
                .Select(member => new { member.UserId, role = member.Role.ToString(), member.IsActive }).ToListAsync(ct));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/invitations", async (Guid workspaceId, CreateInvitationRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await Owns(db, workspaceId, user, ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || request.TargetUserId == Guid.Empty || request.TargetUserId == UserId(user)
                || request.Role is not (WorkspaceRole.Teacher or WorkspaceRole.Student)
                || (request.StudentRecordId.HasValue && request.Role != WorkspaceRole.Student))
                return Results.BadRequest(new { code = "Invitation.InvalidTarget" });
            var existing = await db.Invitations.FindAsync([request.Id], ct);
            if (existing is not null)
                return existing.WorkspaceId == workspaceId && existing.TargetUserId == request.TargetUserId && existing.Role == request.Role
                    && existing.StudentRecordId == request.StudentRecordId
                    ? Results.Ok(InvitationView(existing)) : Results.Conflict(new { code = "Invitation.RequestConflict" });
            if (request.StudentRecordId is Guid studentRecordId && !await db.Students.AnyAsync(student =>
                student.Id == studentRecordId && student.WorkspaceId == workspaceId && student.IsActive && student.UserId == null, ct))
                return Results.Conflict(new { code = "Invitation.StudentUnavailable" });
            if (await db.Memberships.AnyAsync(member => member.WorkspaceId == workspaceId && member.UserId == request.TargetUserId && member.IsActive, ct))
                return Results.Conflict(new { code = "Membership.AlreadyActive" });
            var pending = await db.Invitations.SingleOrDefaultAsync(invitation => invitation.WorkspaceId == workspaceId && invitation.TargetUserId == request.TargetUserId && invitation.Status == InvitationStatus.Pending, ct);
            var now = DateTimeOffset.UtcNow;
            if (pending is not null && now < pending.ExpiresAt)
                return pending.Role == request.Role && pending.StudentRecordId == request.StudentRecordId
                    ? Results.Ok(InvitationView(pending)) : Results.Conflict(new { code = "Invitation.AlreadyPending" });
            if (pending is not null) pending.Expire(now);
            var invitation = WorkspaceInvitation.Create(request.Id, workspaceId, request.TargetUserId, request.Role, now, request.StudentRecordId);
            // Persist expiry first within the same transaction so the unique pending-target index permits a new invitation.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (pending is not null) await db.SaveChangesAsync(ct);
            db.Invitations.Add(invitation);
            Audit(db, workspaceId, UserId(user), invitation.Id, "Invitation.Created");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/invitations", InvitationView(invitation));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/invitations", async (Guid workspaceId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await Owns(db, workspaceId, user, ct)) return Results.NotFound();
            return Results.Ok((await db.Invitations.AsNoTracking().Where(invitation => invitation.WorkspaceId == workspaceId)
                .OrderByDescending(invitation => invitation.ExpiresAt).Take(100).ToListAsync(ct)).Select(InvitationView));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/invitations/{invitationId:guid}/cancel", async (Guid workspaceId, Guid invitationId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await Owns(db, workspaceId, user, ct)) return Results.NotFound();
            var invitation = await db.Invitations.SingleOrDefaultAsync(item => item.Id == invitationId && item.WorkspaceId == workspaceId, ct);
            if (invitation is null) return Results.NotFound();
            if (invitation.Status == InvitationStatus.Cancelled) return Results.NoContent();
            if (invitation.Status != InvitationStatus.Pending) return Results.Conflict(new { code = "Invitation.NotPending" });
            invitation.Cancel();
            Audit(db, workspaceId, UserId(user), invitationId, "Invitation.Cancelled");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapPost("/workspaces/{workspaceId:guid}/members/{memberId:guid}/revoke", async (Guid workspaceId, Guid memberId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await Owns(db, workspaceId, user, ct)) return Results.NotFound();
            var member = await db.Memberships.FindAsync([workspaceId, memberId], ct);
            if (member is null) return Results.NotFound();
            if (member.Role == WorkspaceRole.Owner) return Results.Conflict(new { code = "Membership.OwnerProtected" });
            if (!member.IsActive) return Results.NoContent();
            member.Revoke();
            Audit(db, workspaceId, UserId(user), memberId, "Membership.Revoked");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapGet("/workspaces/{workspaceId:guid}/audit", async (Guid workspaceId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await Owns(db, workspaceId, user, ct)) return Results.NotFound();
            return Results.Ok(await db.AuditEntries.AsNoTracking().Where(entry => entry.WorkspaceId == workspaceId)
                .OrderByDescending(entry => entry.OccurredAt).Take(100)
                .Select(entry => new { entry.Action, entry.OccurredAt }).ToListAsync(ct));
        });

        // A targeted invitation may be accepted with an existing account's other-product token.
        // It never permits access to course data outside this user's participation endpoints.
        var participation = app.MapGroup("/api/course-management/v1/participation").RequireAuthorization("IdentityParticipant");
        participation.AddEndpointFilter<WorkspaceSubscriptionWriteAccessFilter>();
        participation.MapGet("/invitations", async (ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            return Results.Ok(await (from invitation in db.Invitations.AsNoTracking()
                join workspace in db.Workspaces.AsNoTracking() on invitation.WorkspaceId equals workspace.Id
                join subscriptionJoin in db.WorkspaceSubscriptions.AsNoTracking()
                    on workspace.Id equals subscriptionJoin.WorkspaceId into subscriptions
                from subscription in subscriptions.DefaultIfEmpty()
                where invitation.TargetUserId == UserId(user) && invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt > DateTimeOffset.UtcNow
                orderby invitation.ExpiresAt
                select new
                {
                    invitation.Id,
                    invitation.WorkspaceId,
                    workspaceName = workspace.Name,
                    role = invitation.Role.ToString(),
                    invitation.ExpiresAt,
                    isReadOnly = workspace.IsErasureReadOnly || (subscription != null && (subscription.AccessStatus != WorkspaceAccessStatus.Active
                        || subscription.StartsAt > now || subscription.EndsAt <= now))
                }).Take(100).ToListAsync(ct));
        });

        participation.MapPost("/invitations/{invitationId:guid}/accept", async (Guid invitationId, AcceptInvitationRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 200)
                return Results.BadRequest(new { code = "Profile.InvalidName" });
            var invitation = await db.Invitations.SingleOrDefaultAsync(item => item.Id == invitationId && item.TargetUserId == UserId(user), ct);
            if (invitation is null) return Results.NotFound();
            if (invitation.Role == WorkspaceRole.Student)
            {
                if (await db.Students.AnyAsync(student => student.WorkspaceId == invitation.WorkspaceId && !student.IsActive
                    && student.UserId == UserId(user), ct))
                    return Results.Conflict(new { code = "Student.Archived" });
            }
            var member = await db.Memberships.FindAsync([invitation.WorkspaceId, UserId(user)], ct);
            if (invitation.Status == InvitationStatus.Accepted)
                return member?.IsActive == true ? Results.Ok(new { invitation.WorkspaceId, role = member.Role.ToString() }) : Results.Conflict(new { code = "Membership.Revoked" });
            if (!invitation.CanAccept(UserId(user), DateTimeOffset.UtcNow)) return Results.Conflict(new { code = "Invitation.NotAvailable" });
            if (member?.IsActive == true && member.Role != invitation.Role) return Results.Conflict(new { code = "Membership.RoleConflict" });

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            StudentRecord? linkedStudent = null;
            if (invitation.Role == WorkspaceRole.Student)
            {
                await WorkspaceStudentCapacity.LockWorkspaceAsync(db, invitation.WorkspaceId, ct);
                await db.Entry(invitation).ReloadAsync(ct);
                if (invitation.Status == InvitationStatus.Accepted)
                {
                    var acceptedMember = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(item =>
                        item.WorkspaceId == invitation.WorkspaceId && item.UserId == UserId(user), ct);
                    return acceptedMember?.IsActive == true
                        ? Results.Ok(new { invitation.WorkspaceId, role = acceptedMember.Role.ToString() })
                        : Results.Conflict(new { code = "Membership.Revoked" });
                }
                if (!invitation.CanAccept(UserId(user), DateTimeOffset.UtcNow))
                    return Results.Conflict(new { code = "Invitation.NotAvailable" });

                if (member is null)
                    member = await db.Memberships.FindAsync([invitation.WorkspaceId, UserId(user)], ct);
                else
                    await db.Entry(member).ReloadAsync(ct);
                if (member?.IsActive == true && member.Role != invitation.Role)
                    return Results.Conflict(new { code = "Membership.RoleConflict" });

                if (await db.Students.AnyAsync(student => student.WorkspaceId == invitation.WorkspaceId && !student.IsActive
                    && student.UserId == UserId(user), ct))
                    return Results.Conflict(new { code = "Student.Archived" });
                linkedStudent = await db.Students.SingleOrDefaultAsync(student => student.WorkspaceId == invitation.WorkspaceId
                    && student.UserId == UserId(user), ct);
                if (invitation.StudentRecordId is Guid selectedStudentId)
                {
                    var selectedStudent = await db.Students.SingleOrDefaultAsync(student => student.Id == selectedStudentId
                        && student.WorkspaceId == invitation.WorkspaceId && student.IsActive, ct);
                    if (selectedStudent is null || (selectedStudent.UserId is Guid selectedUserId && selectedUserId != UserId(user))
                        || (linkedStudent is not null && linkedStudent.Id != selectedStudent.Id))
                        return Results.Conflict(new { code = "Invitation.StudentUnavailable" });
                    linkedStudent = selectedStudent;
                }
                else if (linkedStudent is null)
                {
                    var studentEmail = user.FindFirstValue("email")?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(studentEmail))
                    {
                        if (await db.Students.AnyAsync(student => student.WorkspaceId == invitation.WorkspaceId && !student.IsActive
                            && student.UserId == null && student.Email == studentEmail, ct))
                            return Results.Conflict(new { code = "Student.Archived" });
                        if (await db.Students.AnyAsync(student => student.WorkspaceId == invitation.WorkspaceId && student.IsActive
                            && student.UserId == null && student.Email == studentEmail, ct))
                            return Results.Conflict(new { code = "Student.RecordSelectionRequired" });
                    }
                }
                if (linkedStudent is null && !await WorkspaceStudentCapacity.CanAddAsync(db, invitation.WorkspaceId, 1, ct))
                    return Results.Conflict(new { code = "Student.ActiveLimitReached" });
            }
            if (member is null) db.Memberships.Add(WorkspaceMembership.Create(invitation.WorkspaceId, UserId(user), invitation.Role));
            else if (!member.IsActive) member.Activate(invitation.Role);
            if (!await db.UserProfiles.AnyAsync(profile => profile.UserId == UserId(user), ct))
                db.UserProfiles.Add(CourseUserProfile.Create(UserId(user), request.DisplayName));
            Guid? linkedStudentId = null;
            if (invitation.Role == WorkspaceRole.Student)
            {
                var email = user.FindFirstValue("email")?.Trim().ToLowerInvariant();
                if (linkedStudent is null)
                {
                    linkedStudent = StudentRecord.Create(Guid.NewGuid(), invitation.WorkspaceId, request.DisplayName,
                        string.IsNullOrWhiteSpace(email) ? null : email, null, DateTimeOffset.UtcNow);
                    db.Students.Add(linkedStudent);
                }
                if (linkedStudent.UserId is null) linkedStudent.LinkAccount(UserId(user));
                linkedStudentId = linkedStudent.Id;
            }

            var now = DateTimeOffset.UtcNow;
            if (linkedStudentId is Guid studentId)
                await CourseManagementReminderScheduling.ScheduleExistingForStudentAsync(db, invitation.WorkspaceId,
                    studentId, UserId(user), now, ct);
            invitation.Accept(UserId(user), now);
            Audit(db, invitation.WorkspaceId, UserId(user), invitationId, "Invitation.Accepted");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { invitation.WorkspaceId, role = invitation.Role.ToString() });
        });
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static Task<bool> Owns(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user, CancellationToken ct) =>
        db.Workspaces.AnyAsync(workspace => workspace.Id == workspaceId && workspace.OwnerUserId == UserId(user), ct);
    private static object WorkspaceView(Workspace workspace) => new { workspace.Id, workspace.Name, type = workspace.Type.ToString(),
        institutionCategory = workspace.InstitutionCategory?.ToString() };
    private static object InvitationView(WorkspaceInvitation invitation) => new { invitation.Id, invitation.WorkspaceId, invitation.TargetUserId,
        invitation.StudentRecordId,
        role = invitation.Role.ToString(), status = invitation.Status.ToString(), invitation.ExpiresAt };
    private static void Audit(CourseManagementDbContext db, Guid workspaceId, Guid actorId, Guid targetId, string action) =>
        db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, actorId, targetId, action));
}

public sealed record CreateWorkspaceRequest(Guid Id, string Name, WorkspaceType Type);
public sealed record CreateInvitationRequest(Guid Id, Guid TargetUserId, WorkspaceRole Role, Guid? StudentRecordId = null);
public sealed record AcceptInvitationRequest(string DisplayName);
