using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Security.Authorization;

namespace CourseManagement.API;

/// <summary>
/// The platform's view of institutions (19.11.2026): a list and a summary, and support's view of an institution's records.
/// Support needs no grant from the owner (user decision, 19.11.2026), only a second factor; every look is logged for the
/// owner. Corrections are in <see cref="SupportChangeEndpoints"/>.
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
                    optedOutAddresses = await db.EmailOptOuts.CountAsync(item => item.WorkspaceId == workspaceId, ct),
                    undeliveredLast30Days = await db.Emails.CountAsync(item => item.WorkspaceId == workspaceId && item.ProcessedAt >= since
                        && (item.Delivery == CourseEmailDelivery.Failed || item.Delivery == CourseEmailDelivery.PartlyFailed), ct)
                },
                supportLog = await db.SupportAccessLogs.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                    .OrderByDescending(item => item.OccurredAt).Take(30)
                    .Select(item => new { item.AdminUserId, section = item.Section.ToString(), item.What, item.IsChange, item.Before, item.After, item.OccurredAt })
                    .ToListAsync(ct)
            });
        });

        // Views of an institution's records for support (no grant since 19.11.2026; second factor required). Each call is logged for the owner.
        admin.MapGet("/workspaces/{workspaceId:guid}/support/students", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, IMfaPolicyStore mfa, CancellationToken ct) =>
        {
            var gate = await OpenGrantAsync(db, mfa, workspaceId, user, SupportAccessSections.Students, "Öğrenci listesi", ct);
            if (gate.Problem is not null) return gate.Problem;
            var students = await db.Students.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.IsActive).ThenBy(item => item.DisplayName).Take(500)
                .Select(item => new { item.Id, item.DisplayName, item.Email, item.PhoneNumber, item.IsActive, item.EmailsMuted,
                    guardians = (from link in db.StudentGuardianLinks
                        join guardian in db.Guardians on link.GuardianContactId equals guardian.Id
                        where link.StudentId == item.Id && link.IsActive
                        select new { guardian.Id, guardian.DisplayName, guardian.Email, guardian.PhoneNumber }).ToList(),
                    groups = (from enrollment in db.Enrollments
                        join courseGroup in db.Groups on enrollment.GroupId equals courseGroup.Id
                        where enrollment.StudentId == item.Id && enrollment.Status == EnrollmentStatus.Active
                        select courseGroup.Name).ToList() })
                .ToListAsync(ct);
            return Results.Ok(students);
        });

        admin.MapGet("/workspaces/{workspaceId:guid}/support/lessons", async (Guid workspaceId, DateOnly? from, int? days,
            ClaimsPrincipal user, CourseManagementDbContext db, IMfaPolicyStore mfa, CancellationToken ct) =>
        {
            if (days is < 1 or > 31) return Results.BadRequest(new { code = "SupportAccess.InvalidRange" });
            var gate = await OpenGrantAsync(db, mfa, workspaceId, user, SupportAccessSections.Schedule, "Takvim ve yoklama", ct);
            if (gate.Problem is not null) return gate.Problem;
            var startDay = from ?? CourseEmailCollector.TurkeyDate(DateTimeOffset.UtcNow).AddDays(-7);
            var start = new DateTimeOffset(startDay.ToDateTime(TimeOnly.MinValue), CourseManagementTime.Turkey.GetUtcOffset(startDay.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
            var end = start.AddDays(days ?? 14);
            var lessons = await (from lesson in db.LessonSessions.AsNoTracking()
                join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                where lesson.WorkspaceId == workspaceId && lesson.StartsAt >= start && lesson.StartsAt < end
                orderby lesson.StartsAt
                select new
                {
                    lesson.Id, courseName = course.Name, lesson.StartsAt, lesson.EndsAt, status = lesson.Status.ToString(), lesson.TeacherUserId,
                    attendance = (from participant in db.LessonParticipants
                        join student in db.Students on participant.StudentId equals student.Id
                        where participant.LessonSessionId == lesson.Id && participant.IsActive
                        select new { studentId = student.Id, student.DisplayName,
                            status = db.AttendanceRecords.Where(record => record.LessonSessionId == lesson.Id && record.StudentId == student.Id)
                                .Select(record => record.Status.ToString()).FirstOrDefault(),
                            charged = db.Installments.Any(item => item.SourceLessonId == lesson.Id && item.StudentId == student.Id) }).ToList(),
                    lesson.SeriesId
                }).Take(500).ToListAsync(ct);
            return Results.Ok(lessons);
        });

        admin.MapGet("/workspaces/{workspaceId:guid}/support/finance", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, IMfaPolicyStore mfa, CancellationToken ct) =>
        {
            var gate = await OpenGrantAsync(db, mfa, workspaceId, user, SupportAccessSections.Finance, "Tahsilat", ct);
            if (gate.Problem is not null) return gate.Problem;
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
            CourseManagementDbContext db, IMfaPolicyStore mfa, CancellationToken ct) =>
        {
            var gate = await OpenGrantAsync(db, mfa, workspaceId, user, SupportAccessSections.Settings, "Ayarlar", ct);
            if (gate.Problem is not null) return gate.Problem;
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

        // The owner's side: every look and every correction eduivme support made.
        api.MapGet("/workspaces/{workspaceId:guid}/support-access", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            var rows = await db.SupportAccessLogs.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.OccurredAt).Take(200).ToListAsync(ct);
            // Names are read now, so an erased student or guardian stays erased here too.
            var targets = rows.Where(item => item.TargetId is not null).Select(item => item.TargetId!.Value).Distinct().ToArray();
            var names = await db.Students.AsNoTracking().Where(item => targets.Contains(item.Id)).Select(item => new { item.Id, Name = item.DisplayName })
                .Concat(db.Guardians.AsNoTracking().Where(item => targets.Contains(item.Id)).Select(item => new { item.Id, Name = item.DisplayName }))
                .ToDictionaryAsync(item => item.Id, item => item.Name, ct);
            var lessonNames = await (from lesson in db.LessonSessions.AsNoTracking()
                    join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                    where targets.Contains(lesson.Id) select new { lesson.Id, course.Name }).ToDictionaryAsync(item => item.Id, item => item.Name, ct);
            var logs = rows.Select(item => new
            {
                item.GrantId, section = item.Section.ToString(), item.What, item.OccurredAt, item.IsChange,
                target = item.TargetId is { } id ? names.GetValueOrDefault(id) ?? lessonNames.GetValueOrDefault(id) : null,
                item.Before, item.After
            });
            return Results.Ok(new { logs });
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());

    }

    /// <summary>
    /// eduivme support may open an institution's records without the owner's approval (user decision, 19.11.2026), so that
    /// a user is never left alone with a mistake. Whether a second factor is needed comes from the administrators' MFA
    /// policy for <see cref="MfaOperationCategories.CourseManagement"/> (every operation, changes only, or off; Identity
    /// unreachable means required). Every look is logged for the owner.
    /// </summary>
    private static async Task<SupportGate> OpenGrantAsync(CourseManagementDbContext db, IMfaPolicyStore mfa, Guid workspaceId,
        ClaimsPrincipal user, SupportAccessSections section, string what, CancellationToken ct)
    {
        var gate = await SupportActorAsync(db, mfa, workspaceId, user, change: false, ct);
        if (gate.AdminId is not { } adminId) return gate;
        db.SupportAccessLogs.Add(SupportAccessLog.View(workspaceId, adminId, section, what, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(ct);
        return gate;
    }

    /// <summary>The administrator looking or correcting, or the answer to give (404 unknown institution, 403 second factor).</summary>
    internal static async Task<SupportGate> SupportActorAsync(CourseManagementDbContext db, IMfaPolicyStore mfa, Guid workspaceId,
        ClaimsPrincipal user, bool change, CancellationToken ct)
    {
        if (!await db.Workspaces.AnyAsync(item => item.Id == workspaceId, ct)) return new SupportGate(null, Results.NotFound());
        var mode = await mfa.GetModeAsync(MfaOperationCategories.CourseManagement, ct);
        var needsMfa = !string.Equals(mode, MfaPolicyModes.Disabled, StringComparison.OrdinalIgnoreCase)
            && (change || !string.Equals(mode, MfaPolicyModes.MutationsOnly, StringComparison.OrdinalIgnoreCase));
        if (needsMfa && !string.Equals(user.FindFirstValue("amr"), "mfa", StringComparison.Ordinal))
            return new SupportGate(null, Results.Json(new { code = "Auth.MfaRequired" }, statusCode: StatusCodes.Status403Forbidden));
        return new SupportGate(AssignmentEndpoints.UserId(user), null);
    }

    private static Task<bool> IsOwner(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = AssignmentEndpoints.UserId(user);
        return db.Workspaces.AnyAsync(item => item.Id == workspaceId && item.OwnerUserId == userId, ct);
    }

}

public sealed record AdminWorkspaceRow(Guid Id, string Name, string Type, DateTimeOffset CreatedAt, int TeacherCount,
    int ActiveStudentCount, DateTimeOffset? LastActivityAt, bool IsReadOnly);

public sealed record SupportGate(Guid? AdminId, IResult? Problem);
