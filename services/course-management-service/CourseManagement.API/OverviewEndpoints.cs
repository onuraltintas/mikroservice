using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

/// <summary>
/// The institution panel's first page (19.11.2026): today's lessons, lessons still waiting for attendance and, for the
/// owner only, overdue installments, this month's collections and yesterday's e-mails. A teacher sees their own lessons.
/// </summary>
public static class OverviewEndpoints
{
    public static void MapOverviewEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/workspaces/{workspaceId:guid}/overview", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = AssignmentEndpoints.UserId(user);
            var owner = await db.Workspaces.AnyAsync(item => item.Id == workspaceId && item.OwnerUserId == userId, ct);
            var teacher = !owner && await db.Memberships.AnyAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId
                && item.IsActive && item.Role == WorkspaceRole.Teacher, ct);
            if (!owner && !teacher) return Results.NotFound();

            var now = DateTimeOffset.UtcNow;
            var today = CourseEmailCollector.TurkeyDate(now);
            var dayStart = Start(today);
            var dayEnd = Start(today.AddDays(1));
            var lessons = db.LessonSessions.AsNoTracking().Where(item => item.WorkspaceId == workspaceId);
            if (teacher) lessons = lessons.Where(item => item.TeacherUserId == userId);

            var todays = await (from lesson in lessons
                join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                where lesson.StartsAt >= dayStart && lesson.StartsAt < dayEnd
                orderby lesson.StartsAt
                select new
                {
                    lesson.Id, CourseName = course.Name, lesson.StartsAt, lesson.EndsAt, Status = lesson.Status.ToString(), lesson.TeacherUserId,
                    GroupName = db.Groups.Where(item => item.Id == lesson.GroupId).Select(item => item.Name).FirstOrDefault(),
                    RoomName = db.Rooms.Where(room => room.Id == lesson.RoomId).Select(room => room.Name).FirstOrDefault(),
                    TeacherName = db.UserProfiles.Where(profile => profile.UserId == lesson.TeacherUserId).Select(profile => profile.DisplayName).FirstOrDefault(),
                    StudentCount = db.LessonParticipants.Count(participant => participant.LessonSessionId == lesson.Id && participant.IsActive),
                    MarkedCount = db.AttendanceRecords.Count(record => record.LessonSessionId == lesson.Id)
                }).ToListAsync(ct);

            var weekAgo = now.AddDays(-7);
            var waiting = await (from lesson in lessons
                join course in db.Courses.AsNoTracking() on lesson.CourseId equals course.Id
                where lesson.Status == LessonSessionStatus.Scheduled && lesson.EndsAt <= now && lesson.StartsAt >= weekAgo
                    && !db.AttendanceRecords.Any(record => record.LessonSessionId == lesson.Id)
                orderby lesson.StartsAt descending
                select new { lesson.Id, CourseName = course.Name, lesson.StartsAt,
                    GroupName = db.Groups.Where(item => item.Id == lesson.GroupId).Select(item => item.Name).FirstOrDefault() })
                .Take(10).ToListAsync(ct);

            object? finance = null;
            object? emails = null;
            if (owner)
            {
                var overdue = await CourseEmailContent.OpenInstallmentsAsync(db, workspaceId, query => query.Where(item => item.DueOn < today), ct);
                var monthStart = Start(new DateOnly(today.Year, today.Month, 1));
                var collected = await db.Payments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.ReceivedAt >= monthStart)
                    .SumAsync(item => (decimal?)item.Amount, ct) ?? 0m;
                finance = new
                {
                    overdueCount = overdue.Count, overdueStudentCount = overdue.Select(item => item.StudentId).Distinct().Count(),
                    overdueAmount = overdue.Sum(item => item.Remaining), monthCollections = collected
                };
                var yesterday = Start(today.AddDays(-1));
                var sent = await db.Emails.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.ProcessedAt >= yesterday
                        && item.ProcessedAt < dayStart)
                    .GroupBy(item => new { item.Kind, item.Status }).Select(group => new { group.Key.Kind, group.Key.Status, Count = group.Count() })
                    .ToListAsync(ct);
                emails = new
                {
                    sent = sent.Where(item => item.Status == CourseEmailStatus.Sent).Sum(item => item.Count),
                    skipped = sent.Where(item => item.Status == CourseEmailStatus.Skipped).Sum(item => item.Count),
                    byKind = sent.Where(item => item.Status == CourseEmailStatus.Sent).GroupBy(item => item.Kind)
                        .Select(group => new { kind = group.Key.ToString(), count = group.Sum(item => item.Count) })
                };
            }

            var pendingSupport = owner && await db.SupportAccessGrants.AnyAsync(item => item.WorkspaceId == workspaceId
                && item.Status == SupportAccessStatus.Requested, ct);
            var activeSupport = owner && (await db.SupportAccessGrants.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && item.Status == SupportAccessStatus.Active).ToListAsync(ct)).Any(item => item.IsActive(now));

            return Results.Ok(new
            {
                today, role = owner ? "Owner" : "Teacher",
                todayLessons = todays.Select(item => new
                {
                    item.Id, item.CourseName, item.GroupName, item.RoomName, item.TeacherName, item.TeacherUserId, item.StartsAt, item.EndsAt,
                    item.StudentCount, attendanceTaken = item.MarkedCount > 0,
                    status = item.Status == nameof(LessonSessionStatus.Scheduled) && item.StartsAt <= now && item.EndsAt > now ? "InProgress" : item.Status
                }),
                lessonsWaitingForAttendance = waiting,
                finance, emails,
                supportAccess = owner ? new { pendingRequest = pendingSupport, active = activeSupport } : null
            });
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());
    }

    private static DateTimeOffset Start(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, CourseManagementTime.Turkey.GetUtcOffset(local)).ToUniversalTime();
    }
}
