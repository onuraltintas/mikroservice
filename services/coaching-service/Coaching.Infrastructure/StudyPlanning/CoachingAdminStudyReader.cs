using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed record AdminStudyPlanSummary(Guid Id, Guid PlanId, int RevisionNumber, int Version,
    string Title, StudyPlanStatus Status, bool IsActive, DateTime CreatedAt, int? AutomaticAvailabilityVersion);
public sealed record AdminStudyPlanPage(IReadOnlyList<AdminStudyPlanSummary> Items, int TotalCount, int PageNumber, int PageSize);

// New student-owned planning data is global-admin read-only; it is not institution-owned content.
public sealed class CoachingAdminStudyReader(CoachingDbContext db, ICoachingAdminScopeAuthorization scope)
{
    private async Task RequireAsync(Guid student, CancellationToken ct)
    {
        if (!(await scope.RequireReadScopeAsync(ct)).IsGlobal)
            throw new BusinessRuleException("Authorization.Forbidden", "Öğrencinin kişisel çalışma planı yalnız global yönetici tarafından incelenebilir.");
        if (student == Guid.Empty) throw new ArgumentException("Student identity is required.");
    }
    public async Task<StudyAvailabilityView?> AvailabilityAsync(Guid student, CancellationToken ct)
    {
        await RequireAsync(student, ct);
        var row = await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student, ct);
        return row is null ? null : new(row.Version, row.TimeZoneId,
            row.Windows.Select(x => new StudyWindowInput(x.Day, x.StartMinute, x.EndMinute)).ToArray());
    }
    public async Task<AdminStudyPlanPage> PlansAsync(Guid student, int pageNumber, int pageSize, StudyPlanStatus? status, string? search, CancellationToken ct)
    {
        await RequireAsync(student, ct);
        if (pageNumber is < 1 or > 10000 || pageSize is < 1 or > 100 || search?.Length > 100 || status.HasValue && !Enum.IsDefined(status.Value))
            throw new ArgumentException("Invalid plan filters.");
        var query = db.StudyPlanRevisions.AsNoTracking().Where(x => x.StudentId == student);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.Title.Contains(term)); }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminStudyPlanSummary(x.Id, x.PlanId, x.RevisionNumber, x.Version, x.Title, x.Status, x.IsActive, x.CreatedAt, x.AutomaticAvailabilityVersion)).ToListAsync(ct);
        return new(rows, count, pageNumber, pageSize);
    }
    public async Task<ManualStudyPlanView?> PlanAsync(Guid student, Guid revisionId, CancellationToken ct)
    {
        await RequireAsync(student, ct);
        var plan = await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student && x.Id == revisionId, ct);
        if (plan is null) return null;
        var tasks = await db.StudyPlanTasks.AsNoTracking().Where(x => x.StudentId == student && x.RevisionId == revisionId)
            .OrderBy(x => x.PlannedDate).ThenBy(x => x.Id).Select(x => new ManualStudyTaskView(x.Id, x.PlannedDate, x.Title,
                x.PlannedMinutes, x.TopicId, x.IsPinned, x.IsCompleted, x.ActualMinutes, x.CompletedAt)).Take(1001).ToListAsync(ct);
        if (tasks.Count > 1000) throw new BusinessRuleException("StudyPlanning.ReportLimit", "Bu revizyon çok fazla görev içeriyor; ayrıntı inceleme sınırı aşıldı.");
        return new(plan.Id, plan.Version, plan.Title, plan.Status, tasks);
    }
    public async Task<StudentStudyReport> ReportAsync(Guid student, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await RequireAsync(student, ct);
        return await CoachingStudentStudyReportService.ReadAsync(db, student, from, to, ct);
    }
}
