using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CourseManagement.API;

/// <summary>
/// The platform's view of institutions (19.11.2026): a list and a summary without personal records, and time-limited,
/// read-only support access that the owner approves (see <see cref="SupportAccessGrant"/>).
/// </summary>
public static class SupportAccessEndpoints
{
    public static void MapSupportAccessEndpoints(this WebApplication app, RouteGroupBuilder api)
    {
        var admin = app.MapGroup("/api/course-management/v1/admin").RequireAuthorization("CourseManagementAdmin");

        admin.MapGet("/workspaces", async (string? query, int? skip, int? take, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (skip is < 0 || take is < 1 or > 100) return Results.BadRequest(new { code = "Workspace.InvalidPage" });
            var workspaces = db.Workspaces.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                workspaces = workspaces.Where(item => EF.Functions.ILike(item.Name, $"%{term.Replace("%", "").Replace("_", "")}%"));
            }
            var total = await workspaces.CountAsync(ct);
            var rows = await workspaces.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
                .Skip(skip ?? 0).Take(take ?? 50)
                .Select(item => new AdminWorkspaceRow(item.Id, item.Name, item.Type.ToString(), item.CreatedAt,
                    db.Memberships.Count(member => member.WorkspaceId == item.Id && member.IsActive && member.Role == WorkspaceRole.Teacher),
                    db.Students.Count(student => student.WorkspaceId == item.Id && student.IsActive),
                    db.AuditEntries.Where(entry => entry.WorkspaceId == item.Id).Max(entry => (DateTimeOffset?)entry.OccurredAt),
                    item.IsErasureReadOnly))
                .ToListAsync(ct);
            return Results.Ok(new { total, items = rows });
        });

        admin.MapGet("/workspaces/{workspaceId:guid}", async (Guid workspaceId, ClaimsPrincipal user, CourseManagementDbContext db,
            CancellationToken ct) =>
        {
            var workspace = await db.Workspaces.AsNoTracking().SingleOrDefaultAsync(item => item.Id == workspaceId, ct);
            if (workspace is null) return Results.NotFound();
            var since = DateTimeOffset.UtcNow.AddDays(-30);
            var emails = await db.Emails.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.ProcessedAt >= since)
                .GroupBy(item => item.Status).Select(group => new { Status = group.Key, Count = group.Count(), People = group.Sum(item => item.RecipientCount) })
                .ToListAsync(ct);
            var settings = await db.EmailSettings.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId, ct);
            var grants = await db.SupportAccessGrants.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.RequestedAt).Take(20).ToListAsync(ct);
            var now = DateTimeOffset.UtcNow;
            return Results.Ok(new
            {
                workspace.Id, workspace.Name, type = workspace.Type.ToString(), workspace.CreatedAt, workspace.OwnerUserId,
                isReadOnly = workspace.IsErasureReadOnly,
                teacherCount = await db.Memberships.CountAsync(item => item.WorkspaceId == workspaceId && item.IsActive
                    && item.Role == WorkspaceRole.Teacher, ct),
                activeStudentCount = await db.Students.CountAsync(item => item.WorkspaceId == workspaceId && item.IsActive, ct),
                courseCount = await db.Courses.CountAsync(item => item.WorkspaceId == workspaceId, ct),
                lessonsLast30Days = await db.LessonSessions.CountAsync(item => item.WorkspaceId == workspaceId
                    && item.StartsAt >= since && item.StartsAt <= now, ct),
                email = new
                {
                    switchedOn = settings is not null && Enum.GetValues<CourseEmailKind>().Any(settings.IsEnabled),
                    sentLast30Days = emails.Where(item => item.Status == CourseEmailStatus.Sent).Sum(item => item.Count),
                    recipientsLast30Days = emails.Where(item => item.Status == CourseEmailStatus.Sent).Sum(item => item.People),
                    skippedLast30Days = emails.Where(item => item.Status == CourseEmailStatus.Skipped).Sum(item => item.Count),
                    optedOutAddresses = await db.EmailOptOuts.CountAsync(item => item.WorkspaceId == workspaceId, ct)
                },
                supportAccess = grants.Select(grant => GrantView(grant, now)).ToArray()
            });
        });

        admin.MapPost("/workspaces/{workspaceId:guid}/support-access", async (Guid workspaceId, RequestSupportAccessRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await db.Workspaces.AnyAsync(item => item.Id == workspaceId, ct)) return Results.NotFound();
            var adminId = AssignmentEndpoints.UserId(user);
            var now = DateTimeOffset.UtcNow;
            var open = await db.SupportAccessGrants.Where(item => item.WorkspaceId == workspaceId && item.AdminUserId == adminId
                && item.Status != SupportAccessStatus.Ended).ToListAsync(ct);
            foreach (var grant in open.Where(grant => grant.Status == SupportAccessStatus.Active && !grant.IsActive(now)))
                grant.End("Expired", now);
            var stillOpen = open.FirstOrDefault(grant => grant.Status != SupportAccessStatus.Ended);
            if (stillOpen is not null) return Results.Conflict(new { code = "SupportAccess.AlreadyOpen", grant = GrantView(stillOpen, now) });
            SupportAccessGrant created;
            try { created = SupportAccessGrant.Request(workspaceId, adminId, request.Reason ?? string.Empty, now); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "SupportAccess.ReasonRequired" }); }
            db.SupportAccessGrants.Add(created);
            AssignmentEndpoints.AddAudit(db, workspaceId, adminId, created.Id, "SupportAccess.Requested");
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            { return Results.Conflict(new { code = "SupportAccess.AlreadyOpen" }); }
            return Results.Created($"/api/course-management/v1/admin/workspaces/{workspaceId}/support-access/{created.Id}", GrantView(created, now));
        });

        admin.MapPost("/workspaces/{workspaceId:guid}/support-access/{grantId:guid}/end", async (Guid workspaceId, Guid grantId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var adminId = AssignmentEndpoints.UserId(user);
            var grant = await db.SupportAccessGrants.SingleOrDefaultAsync(item => item.Id == grantId && item.WorkspaceId == workspaceId
                && item.AdminUserId == adminId, ct);
            if (grant is null) return Results.NotFound();
            grant.End("EndedBySupport", DateTimeOffset.UtcNow);
            AssignmentEndpoints.AddAudit(db, workspaceId, adminId, grant.Id, "SupportAccess.Ended");
            await db.SaveChangesAsync(ct);
            return Results.Ok(GrantView(grant, DateTimeOffset.UtcNow));
        });

        // Read-only views under an approved grant. Each call is logged for the owner.
        admin.MapGet("/workspaces/{workspaceId:guid}/support/students", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var grant = await OpenGrantAsync(db, workspaceId, user, SupportAccessSections.Students, "Öğrenci listesi", ct);
            if (grant is null) return Results.NotFound();
            var students = await db.Students.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.IsActive).ThenBy(item => item.DisplayName).Take(500)
                .Select(item => new { item.Id, item.DisplayName, item.Email, item.PhoneNumber, item.IsActive, item.EmailsMuted,
                    guardians = (from link in db.StudentGuardianLinks
                        join guardian in db.Guardians on link.GuardianContactId equals guardian.Id
                        where link.StudentId == item.Id && link.IsActive
                        select new { guardian.DisplayName, guardian.Email, guardian.PhoneNumber }).ToList(),
                    groups = (from enrollment in db.Enrollments
                        join courseGroup in db.Groups on enrollment.GroupId equals courseGroup.Id
                        where enrollment.StudentId == item.Id && enrollment.Status == EnrollmentStatus.Active
                        select courseGroup.Name).ToList() })
                .ToListAsync(ct);
            return Results.Ok(students);
        });

        admin.MapGet("/workspaces/{workspaceId:guid}/support/lessons", async (Guid workspaceId, DateOnly? from, int? days,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (days is < 1 or > 31) return Results.BadRequest(new { code = "SupportAccess.InvalidRange" });
            var grant = await OpenGrantAsync(db, workspaceId, user, SupportAccessSections.Schedule, "Takvim ve yoklama", ct);
            if (grant is null) return Results.NotFound();
            var startDay = from ?? CourseEmailCollector.TurkeyDate(DateTimeOffset.UtcNow).AddDays(-7);
            var start = new DateTimeOffset(startDay.ToDateTime(TimeOnly.MinValue), CourseManagementTime.Turkey.GetUtcOffset(startDay.ToDateTime(TimeOnly.MinValue)));
            var end = start.AddDays(days ?? 14);
            var lessons = await (from lesson in db.LessonSessions.AsNoTracking()
                join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                where lesson.WorkspaceId == workspaceId && lesson.StartsAt >= start && lesson.StartsAt < end
                orderby lesson.StartsAt
                select new
                {
                    lesson.Id, courseName = course.Name, lesson.StartsAt, lesson.EndsAt, status = lesson.Status.ToString(), lesson.TeacherUserId,
                    attendance = (from record in db.AttendanceRecords
                        join student in db.Students on record.StudentId equals student.Id
                        where record.LessonSessionId == lesson.Id
                        select new { student.DisplayName, status = record.Status.ToString() }).ToList()
                }).Take(500).ToListAsync(ct);
            return Results.Ok(lessons);
        });

        admin.MapGet("/workspaces/{workspaceId:guid}/support/finance", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var grant = await OpenGrantAsync(db, workspaceId, user, SupportAccessSections.Finance, "Tahsilat", ct);
            if (grant is null) return Results.NotFound();
            var open = await CourseEmailContent.OpenInstallmentsAsync(db, workspaceId, query => query, ct);
            var names = await db.Students.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, ct);
            var since = DateTimeOffset.UtcNow.AddDays(-60);
            var payments = await db.Payments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.ReceivedAt >= since)
                .OrderByDescending(item => item.ReceivedAt).Take(300)
                .Select(item => new { item.Id, item.StudentId, item.Amount, item.ReceivedAt, method = item.Method.ToString(), isReversal = item.ReversalOfPaymentId != null })
                .ToListAsync(ct);
            return Results.Ok(new
            {
                openInstallments = open.OrderBy(item => item.DueOn).Select(item => new { item.Id, student = names.GetValueOrDefault(item.StudentId),
                    item.CourseName, item.Label, item.Remaining, item.DueOn }),
                payments = payments.Select(item => new { item.Id, student = names.GetValueOrDefault(item.StudentId), item.Amount, item.ReceivedAt,
                    item.method, item.isReversal })
            });
        });

        admin.MapGet("/workspaces/{workspaceId:guid}/support/settings", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var grant = await OpenGrantAsync(db, workspaceId, user, SupportAccessSections.Settings, "Ayarlar", ct);
            if (grant is null) return Results.NotFound();
            var settings = await db.EmailSettings.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId, ct)
                ?? WorkspaceEmailSettings.CreateDefault(workspaceId, DateTimeOffset.UtcNow);
            return Results.Ok(new
            {
                emailRules = settings.Rules.Select(rule => new { kind = rule.Kind.ToString(), rule.Enabled, audience = rule.Audience.ToString() }),
                settings.InstallmentDaysBefore, settings.InstallmentOnDueDay, settings.OverdueDaysAfter, settings.SendHour,
                settings.InformedConsentAt,
                branches = await db.Branches.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).CountAsync(ct),
                rooms = await db.Rooms.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).CountAsync(ct),
                recentEmails = await db.Emails.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.Status != CourseEmailStatus.Pending)
                    .OrderByDescending(item => item.ProcessedAt).Take(50)
                    .Select(item => new { kind = item.Kind.ToString(), status = item.Status.ToString(), item.RecipientCount, item.SkipReason, item.ProcessedAt })
                    .ToListAsync(ct)
            });
        });

        // The owner's side: requests, decisions, the history and every look.
        api.MapGet("/workspaces/{workspaceId:guid}/support-access", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            var grants = await db.SupportAccessGrants.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.RequestedAt).Take(50).ToListAsync(ct);
            var logs = await db.SupportAccessLogs.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.OccurredAt).Take(200)
                .Select(item => new { item.GrantId, section = item.Section.ToString(), item.What, item.OccurredAt }).ToListAsync(ct);
            return Results.Ok(new { grants = grants.Select(grant => GrantView(grant, now)), logs });
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());

        api.MapPost("/workspaces/{workspaceId:guid}/support-access/{grantId:guid}/approve", async (Guid workspaceId, Guid grantId,
            ApproveSupportAccessRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            var grant = await db.SupportAccessGrants.SingleOrDefaultAsync(item => item.Id == grantId && item.WorkspaceId == workspaceId, ct);
            if (grant is null) return Results.NotFound();
            var sections = (request.Sections ?? []).Aggregate(SupportAccessSections.None, (all, item) => all | item);
            var now = DateTimeOffset.UtcNow;
            try { grant.Approve(AssignmentEndpoints.UserId(user), request.Hours, sections, now); }
            catch (InvalidOperationException) { return Results.Conflict(new { code = "SupportAccess.NotRequested" }); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "SupportAccess.InvalidChoice" }); }
            AssignmentEndpoints.AddAudit(db, workspaceId, AssignmentEndpoints.UserId(user), grant.Id, "SupportAccess.Approved");
            await db.SaveChangesAsync(ct);
            return Results.Ok(GrantView(grant, now));
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());

        api.MapPost("/workspaces/{workspaceId:guid}/support-access/{grantId:guid}/decline", async (Guid workspaceId, Guid grantId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            var grant = await db.SupportAccessGrants.SingleOrDefaultAsync(item => item.Id == grantId && item.WorkspaceId == workspaceId, ct);
            if (grant is null) return Results.NotFound();
            try { grant.Decline(AssignmentEndpoints.UserId(user), DateTimeOffset.UtcNow); }
            catch (InvalidOperationException) { return Results.Conflict(new { code = "SupportAccess.NotRequested" }); }
            AssignmentEndpoints.AddAudit(db, workspaceId, AssignmentEndpoints.UserId(user), grant.Id, "SupportAccess.Declined");
            await db.SaveChangesAsync(ct);
            return Results.Ok(GrantView(grant, DateTimeOffset.UtcNow));
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());

        api.MapPost("/workspaces/{workspaceId:guid}/support-access/{grantId:guid}/end", async (Guid workspaceId, Guid grantId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            var grant = await db.SupportAccessGrants.SingleOrDefaultAsync(item => item.Id == grantId && item.WorkspaceId == workspaceId, ct);
            if (grant is null) return Results.NotFound();
            grant.End("EndedByOwner", DateTimeOffset.UtcNow);
            AssignmentEndpoints.AddAudit(db, workspaceId, AssignmentEndpoints.UserId(user), grant.Id, "SupportAccess.Ended");
            await db.SaveChangesAsync(ct);
            return Results.Ok(GrantView(grant, DateTimeOffset.UtcNow));
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());
    }

    /// <summary>The caller's active grant that opens the section, after logging the look; null answers 404.</summary>
    private static async Task<SupportAccessGrant?> OpenGrantAsync(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user,
        SupportAccessSections section, string what, CancellationToken ct)
    {
        // Support access needs a second factor on this sign-in, not only an administrator's password.
        if (!string.Equals(user.FindFirstValue("amr"), "mfa", StringComparison.Ordinal)) return null;
        var adminId = AssignmentEndpoints.UserId(user);
        var now = DateTimeOffset.UtcNow;
        var grants = await db.SupportAccessGrants.Where(item => item.WorkspaceId == workspaceId && item.AdminUserId == adminId
            && item.Status == SupportAccessStatus.Active).ToListAsync(ct);
        var grant = grants.FirstOrDefault(item => item.Opens(section, adminId, now));
        if (grant is null) return null;
        db.SupportAccessLogs.Add(SupportAccessLog.Create(grant, section, what, now));
        await db.SaveChangesAsync(ct);
        return grant;
    }

    private static Task<bool> IsOwner(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = AssignmentEndpoints.UserId(user);
        return db.Workspaces.AnyAsync(item => item.Id == workspaceId && item.OwnerUserId == userId, ct);
    }

    private static SupportGrantView GrantView(SupportAccessGrant grant, DateTimeOffset now) => new(grant.Id, grant.WorkspaceId,
        grant.AdminUserId, grant.Reason, grant.Status == SupportAccessStatus.Active && !grant.IsActive(now) ? "Ended" : grant.Status.ToString(),
        Enum.GetValues<SupportAccessSections>().Where(item => item != SupportAccessSections.None && grant.Sections.HasFlag(item))
            .Select(item => item.ToString()).ToArray(),
        grant.DurationHours, grant.RequestedAt, grant.ApprovedAt, grant.ExpiresAt,
        grant.Status == SupportAccessStatus.Active && !grant.IsActive(now) ? grant.ExpiresAt : grant.EndedAt,
        grant.Status == SupportAccessStatus.Active && !grant.IsActive(now) ? "Expired" : grant.EndReason);
}

public sealed record AdminWorkspaceRow(Guid Id, string Name, string Type, DateTimeOffset CreatedAt, int TeacherCount,
    int ActiveStudentCount, DateTimeOffset? LastActivityAt, bool IsReadOnly);
public sealed record RequestSupportAccessRequest(string? Reason);
public sealed record ApproveSupportAccessRequest(int Hours, SupportAccessSections[]? Sections);
public sealed record SupportGrantView(Guid Id, Guid WorkspaceId, Guid AdminUserId, string Reason, string Status, string[] Sections,
    int DurationHours, DateTimeOffset RequestedAt, DateTimeOffset? ApprovedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? EndedAt,
    string? EndReason);
