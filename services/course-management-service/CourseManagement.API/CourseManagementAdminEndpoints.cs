using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CourseManagement.API;

public static class CourseManagementAdminEndpoints
{
    public static void MapCourseManagementAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/course-management/v1/admin")
            .RequireAuthorization("CourseManagementAdmin");

        admin.MapGet("/overview", async (CourseManagementDbContext db, CancellationToken ct) =>
        {
            var workspaceCounts = await db.Workspaces.AsNoTracking()
                .GroupBy(workspace => workspace.Type)
                .Select(group => new { Type = group.Key, Count = group.Count() })
                .ToListAsync(ct);
            var now = DateTimeOffset.UtcNow;

            return Results.Ok(new
            {
                workspaceCount = workspaceCounts.Sum(item => item.Count),
                institutionWorkspaceCount = workspaceCounts
                    .Where(item => item.Type == WorkspaceType.Institution).Select(item => item.Count).FirstOrDefault(),
                independentTeacherWorkspaceCount = workspaceCounts
                    .Where(item => item.Type == WorkspaceType.IndependentTeacher).Select(item => item.Count).FirstOrDefault(),
                activeMembershipCount = await db.Memberships.AsNoTracking().CountAsync(member => member.IsActive, ct),
                activeStudentCount = await db.Students.AsNoTracking().CountAsync(student => student.IsActive, ct),
                courseCount = await db.Courses.AsNoTracking().CountAsync(ct),
                scheduledLessonCount = await db.LessonSessions.AsNoTracking()
                    .CountAsync(lesson => lesson.Status == LessonSessionStatus.Scheduled, ct),
                pendingInvitationCount = await db.Invitations.AsNoTracking()
                    .CountAsync(invitation => invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt > now, ct)
            });
        });

        admin.MapGet("/subscriptions", async (int? skip, int? take, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var offset = skip ?? 0;
            var pageSize = take ?? 50;
            if (offset < 0 || pageSize is < 1 or > 100)
                return Results.BadRequest(new { code = "Subscription.InvalidPagination" });

            var totalCount = await db.Workspaces.AsNoTracking().CountAsync(ct);
            var rows = await (from workspace in db.Workspaces.AsNoTracking()
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
                }).Skip(offset).Take(pageSize).ToListAsync(ct);

            return Results.Ok(new
            {
                totalCount,
                skip = offset,
                take = pageSize,
                items = rows.Select(row => new
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
                })
            });
        });

        admin.MapGet("/subscription-audit", async (int? skip, int? take, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var offset = skip ?? 0;
            var pageSize = take ?? 50;
            if (offset < 0 || pageSize is < 1 or > 100)
                return Results.BadRequest(new { code = "SubscriptionAudit.InvalidPagination" });

            var auditQuery = from entry in db.AuditEntries.AsNoTracking()
                join workspace in db.Workspaces.AsNoTracking() on entry.WorkspaceId equals workspace.Id
                where entry.Action == "Subscription.Assigned" || entry.Action == "Subscription.Updated"
                select new
                {
                    workspace.Name,
                    workspace.Type,
                    entry.Id,
                    entry.Action,
                    entry.OccurredAt
                };
            var totalCount = await auditQuery.CountAsync(ct);
            var items = await auditQuery.OrderByDescending(entry => entry.OccurredAt)
                .ThenByDescending(entry => entry.Id)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync(ct);

            return Results.Ok(new
            {
                totalCount,
                skip = offset,
                take = pageSize,
                items = items.Select(entry => new
                {
                    workspaceName = entry.Name,
                    workspaceType = entry.Type.ToString(),
                    action = entry.Action,
                    occurredAt = entry.OccurredAt
                })
            });
        });

        admin.MapPut("/workspaces/{workspaceId:guid}/subscription", async (Guid workspaceId,
            AssignWorkspaceSubscriptionRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.PlanName) || request.ActiveStudentLimit < 1
                || request.StartsAt is null || request.EndsAt is null || request.AccessStatus is null
                || !Enum.IsDefined(request.AccessStatus.Value))
                return Results.BadRequest(new { code = "Subscription.InvalidInput" });

            WorkspaceSubscription candidate;
            try
            {
                candidate = WorkspaceSubscription.Create(workspaceId, request.PlanName, request.ActiveStudentLimit,
                    request.StartsAt.Value, request.EndsAt.Value, request.AccessStatus.Value);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { code = "Subscription.InvalidInput" });
            }

            if (!await db.Workspaces.AnyAsync(workspace => workspace.Id == workspaceId, ct))
                return Results.NotFound();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await WorkspaceStudentCapacity.LockWorkspaceAsync(db, workspaceId, ct);
            var activeStudentCount = await db.Students.AsNoTracking()
                .CountAsync(student => student.WorkspaceId == workspaceId && student.IsActive, ct);
            if (activeStudentCount > candidate.ActiveStudentLimit)
                return Results.Conflict(new { code = "Subscription.LimitBelowActiveStudentCount" });

            var subscription = await db.WorkspaceSubscriptions.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId, ct);
            var action = subscription is null ? "Subscription.Assigned" : "Subscription.Updated";
            if (subscription is null)
                db.WorkspaceSubscriptions.Add(candidate);
            else
                subscription.Update(candidate.PlanName, candidate.ActiveStudentLimit, candidate.StartsAt,
                    candidate.EndsAt, candidate.AccessStatus);

            db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId,
                Guid.Parse(user.FindFirstValue("sub")!), workspaceId, action));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return Results.Conflict(new { code = "Subscription.ConcurrentUpdate" });
            }
            await transaction.CommitAsync(ct);

            return Results.Ok(new
            {
                workspaceId = subscription?.WorkspaceId ?? candidate.WorkspaceId,
                planName = candidate.PlanName,
                activeStudentLimit = candidate.ActiveStudentLimit,
                startsAt = candidate.StartsAt,
                endsAt = candidate.EndsAt,
                accessStatus = candidate.AccessStatus.ToString()
            });
        });
    }
}

public sealed record AssignWorkspaceSubscriptionRequest(string? PlanName, int ActiveStudentLimit,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, WorkspaceAccessStatus? AccessStatus);
