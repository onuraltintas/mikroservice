using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingStudentStudyReportService(CoachingDbContext db, ICoachingAccessPolicy access) : IStudentStudyReportService
{
    public async Task<StudentStudyReport> GetAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu rapor yalnız öğrencinin kendi hesabında kullanılabilir.");
        StudyReportCalculator.ValidatePeriod(fromDate, toDate);
        var rows = await (from task in db.StudyPlanTasks.AsNoTracking()
            join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
            where task.StudentId == student && plan.StudentId == student
                && task.PlannedDate >= fromDate && task.PlannedDate <= toDate
                && (plan.Status == StudyPlanStatus.Active || plan.Status == StudyPlanStatus.Archived && task.IsCompleted)
            select new { plan.Status, task.PlannedDate, task.TopicId, task.PlannedMinutes, task.IsCompleted, task.ActualMinutes })
            .ToListAsync(cancellationToken);
        return StudyReportCalculator.Calculate(fromDate, toDate, rows.Select(x => new StudyReportTask(x.Status,
            x.PlannedDate, x.TopicId, x.PlannedMinutes, x.IsCompleted, x.ActualMinutes)).ToArray());
    }
}
