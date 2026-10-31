using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed record InstitutionTeacherWorkload(Guid TeacherUserId, string Name, int Students, int AwaitingGrading, int Overdue,
    int UnreadMessages, int UpcomingSessions);

public sealed record InstitutionWorkload(Guid InstitutionId, int Students, int Teachers, int StudentsWithoutTeacher, int AwaitingGrading,
    int Overdue, int UnreadMessages, int PendingInvitations, IReadOnlyList<InstitutionTeacherWorkload> TeacherRows, DateTime GeneratedAt);

public sealed record InstitutionBranchSummary(Guid InstitutionId, string Name, bool IsCurrent, int Students, int Teachers,
    int AwaitingGrading, int Overdue);

/// <summary>
/// Roadmap O13: the institution's teachers with their students and pending work (grading, overdue assignments,
/// unread messages, sessions in the next 7 days), and a summary row per branch for admins of several institutions.
/// </summary>
public sealed class CoachingInstitutionWorkloadService(CoachingDbContext db, ICoachingAccountDirectory accounts, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<InstitutionWorkload> GetAsync(Guid actor, Guid institutionId, CancellationToken ct)
    {
        await RequireAdminAsync(actor, institutionId, ct);
        var now = Now;
        var teacherIds = await Members(institutionId, CoachingMemberRole.Teacher).ToListAsync(ct);
        var studentIds = await Members(institutionId, CoachingMemberRole.Student).ToListAsync(ct);
        var links = await db.CoachingTeacherStudentAssignments.AsNoTracking()
            .Where(x => x.InstitutionId == institutionId && x.IsActive && Enumerable.Contains(studentIds, x.StudentUserId))
            .Select(x => new { x.TeacherUserId, x.StudentUserId }).ToListAsync(ct);
        // K2 (31.10.2026): only work of students the teacher still teaches here, as on the teacher's own screens.
        var linked = links.Select(x => (x.TeacherUserId, x.StudentUserId)).ToHashSet();
        var work = (await (from link in db.AssignmentStudents.AsNoTracking()
                join assignment in db.Assignments.AsNoTracking() on link.AssignmentId equals assignment.Id
                where assignment.InstitutionId == institutionId && assignment.Status == AssignmentStatus.Active
                select new { assignment.TeacherId, link.StudentId, link.Status, assignment.DueDate })
            .ToListAsync(ct)).Where(x => linked.Contains((x.TeacherId, x.StudentId))).ToList();
        var unread = await db.Set<Coaching.Domain.Entities.CoachingConversation>().AsNoTracking()
            .Where(x => x.InstitutionId == institutionId && x.MessageCount > 0 && (x.TeacherReadAt == null || x.LastMessageAt > x.TeacherReadAt))
            .GroupBy(x => x.TeacherUserId).Select(x => new { Teacher = x.Key, Count = x.Count() }).ToListAsync(ct);
        var until = now.AddDays(7);
        var sessions = await db.CoachingSessions.AsNoTracking()
            .Where(x => Enumerable.Contains(teacherIds, x.TeacherId) && x.Status == SessionStatus.Scheduled && x.ScheduledDate >= now && x.ScheduledDate < until)
            .GroupBy(x => x.TeacherId).Select(x => new { Teacher = x.Key, Count = x.Count() }).ToListAsync(ct);
        var names = (await accounts.GetAccountsAsync(teacherIds, ct)).ToDictionary(x => x.UserId, x => $"{x.FirstName} {x.LastName}".Trim());

        static bool Awaiting(StudentAssignmentStatus status) => status == StudentAssignmentStatus.Submitted;
        bool Overdue(StudentAssignmentStatus status, DateTime due) =>
            status is StudentAssignmentStatus.Assigned or StudentAssignmentStatus.InProgress && due < now;
        var rows = teacherIds.Select(teacher => new InstitutionTeacherWorkload(teacher, names.GetValueOrDefault(teacher) ?? "Öğretmen",
                links.Where(x => x.TeacherUserId == teacher).Select(x => x.StudentUserId).Distinct().Count(),
                work.Count(x => x.TeacherId == teacher && Awaiting(x.Status)),
                work.Count(x => x.TeacherId == teacher && Overdue(x.Status, x.DueDate)),
                unread.SingleOrDefault(x => x.Teacher == teacher)?.Count ?? 0,
                sessions.SingleOrDefault(x => x.Teacher == teacher)?.Count ?? 0))
            .OrderByDescending(x => x.AwaitingGrading + x.Overdue + x.UnreadMessages).ThenBy(x => x.Name, StringComparer.Create(new("tr-TR"), true))
            .ToArray();
        var pendingInvitations = await db.CoachingInvitations.AsNoTracking()
            .CountAsync(x => x.InstitutionId == institutionId && x.Status == CoachingInvitationStatus.Pending && x.ExpiresAt > now, ct);
        return new(institutionId, studentIds.Count, teacherIds.Count, studentIds.Count(s => links.All(x => x.StudentUserId != s)),
            work.Count(x => Awaiting(x.Status)), work.Count(x => Overdue(x.Status, x.DueDate)), rows.Sum(x => x.UnreadMessages),
            pendingInvitations, rows, now);
    }

    public async Task<IReadOnlyList<InstitutionBranchSummary>> BranchesAsync(Guid actor, CancellationToken ct)
    {
        var scope = await accounts.AuthorizeAdminAsync(actor, ct) ?? throw Forbidden();
        var branches = scope.Institutions.Count > 0 ? scope.Institutions
            : scope.InstitutionId is { } only ? [new CoachingAdminInstitutionRef(only, "Kurum")] : [];
        var now = Now; var result = new List<InstitutionBranchSummary>();
        foreach (var branch in branches.Take(50))
        {
            var students = await Members(branch.Id, CoachingMemberRole.Student).CountAsync(ct);
            var teachers = await Members(branch.Id, CoachingMemberRole.Teacher).CountAsync(ct);
            var work = await (from link in db.AssignmentStudents.AsNoTracking()
                    join assignment in db.Assignments.AsNoTracking() on link.AssignmentId equals assignment.Id
                    where assignment.InstitutionId == branch.Id && assignment.Status == AssignmentStatus.Active
                        && (link.Status == StudentAssignmentStatus.Submitted
                            || (link.Status == StudentAssignmentStatus.Assigned || link.Status == StudentAssignmentStatus.InProgress) && assignment.DueDate < now)
                    select link.Status).ToListAsync(ct);
            result.Add(new(branch.Id, branch.Name, branch.Id == scope.InstitutionId, students, teachers,
                work.Count(x => x == StudentAssignmentStatus.Submitted), work.Count(x => x != StudentAssignmentStatus.Submitted)));
        }
        return result;
    }

    private IQueryable<Guid> Members(Guid institutionId, CoachingMemberRole role) =>
        db.CoachingInstitutionMemberships.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.Role == role && x.IsActive).Select(x => x.UserId);

    private async Task RequireAdminAsync(Guid actor, Guid institutionId, CancellationToken ct)
    {
        var scope = await accounts.AuthorizeAdminAsync(actor, ct);
        if (institutionId == Guid.Empty || scope is null || !scope.IsGlobal && !scope.Manages(institutionId)
            || !await accounts.IsActiveInstitutionAsync(institutionId, ct)) throw Forbidden();
    }

    private static BusinessRuleException Forbidden() => new("Authorization.Forbidden", "Bu kurumun raporunu görme yetkiniz yok.");
}
