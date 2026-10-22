using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingPracticeProgressService(CoachingDbContext db, ICoachingAccessPolicy access,
    CoachingStudyPlanAccess staff, TimeProvider? clock = null)
{
    public async Task<PracticeProgressView> GetAsync(Guid? studentId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default)
    {
        var student = studentId ?? access.CurrentUserId ?? Guid.Empty;
        if (studentId.HasValue) await staff.RequireAsync(student, ct);
        else if (!access.IsCurrentStudent(student)) throw new BusinessRuleException("Authorization.Forbidden", "Öğrenci oturumu gereklidir.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(()=>ReadAsync(student,fromDate,toDate,ct));
    }

    private async Task<PracticeProgressView> ReadAsync(Guid student, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var active = await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student && x.Status == StudyPlanStatus.Active, ct);
        var availability = await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student, ct);
        var weekly = await db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student, ct);
        var zoneId = active?.WeeklySource?.TimeZoneId ?? availability?.TimeZoneId ?? weekly?.TimeZoneId ?? "Europe/Istanbul";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var from = fromDate ?? today.AddDays(-89); var to = toDate ?? today;
        StudyReportCalculator.ValidatePeriod(from, to);
        if (to > today) throw new ArgumentException("Rapor bitişi bugünden sonra olamaz.");
        var start = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var results = await db.ExamResults.AsNoTracking().Include(x => x.Exam)
            .Where(x => x.StudentId == student && x.PracticeExamJson != null && x.Exam.ExamDate >= start && x.Exam.ExamDate < end && x.Exam.ExamDate <= now.UtcDateTime)
            .OrderByDescending(x => x.Exam.ExamDate).ThenBy(x => x.Id).Take(1001).ToArrayAsync(ct);
        if (results.Length > 1000) throw new BusinessRuleException("StudyPlanning.ReportLimit", "Deneme sayısı çok fazla; tarih aralığını daraltın.");
        var trends = PracticeProgressCalculator.Calculate(results.Select(x => new PracticeEvidence(x.ExamId, x.Exam.Title, x.Exam.ExamDate,
            x.Exam.StudentOwnerId.HasValue ? "StudentReported" : "TeacherRecorded", x.GetPracticeExam()!, x.CreatedAt)));
        var goals = await db.AcademicGoals.AsNoTracking().Where(x => x.StudentId == student && !x.IsCompleted).OrderBy(x => x.Id).Take(1001).ToArrayAsync(ct);
        if (goals.Length > 1000) throw new BusinessRuleException("StudyPlanning.GoalReportLimit", "Hedef sayısı rapor sınırını aşıyor.");
        var primaryGoalId = await db.Set<StudentPrimaryGoal>().AsNoTracking().Where(x => x.StudentId == student).Select(x => (Guid?)x.GoalId).SingleOrDefaultAsync(ct);
        if (primaryGoalId.HasValue) goals = goals.Where(x => x.Id == primaryGoalId).ToArray();
        var catalogIds = goals.SelectMany(x => new[] { x.TargetSchoolId, x.TargetUniversityProgramId }).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var catalog = await db.TargetCatalog.AsNoTracking().Where(x => Enumerable.Contains(catalogIds, x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var targets = goals.SelectMany(goal => (goal.NetTargets.Count > 0
            ? goal.NetTargets.Select(x => (Code: (string?)x.ExamCode, Net: (decimal?)x.TargetNet))
            : [(Code: (string?)null, Net: (decimal?)null)]).Select(metric => {
            var item = catalog.GetValueOrDefault(goal.TargetSchoolId ?? goal.TargetUniversityProgramId ?? Guid.Empty);
            return new PracticeGoalView(goal.Id, goal.Title, metric.Code, metric.Net,
                item is TargetUniversityProgram program ? program.UniversityName + " — " + program.Name : item?.Name,
                item?.CatalogYear, item?.ScoreYear, item?.PlacementStage, item?.MinimumScore, item?.Rank, item?.Percentile,
                metric.Net is { } target ? trends.Where(x => x.ExamCode == metric.Code).Select(x => {
                    var latest = x.Points[^1]; return new GoalNetComparison(x.Source, latest.ExamDate, latest.Net, Math.Max(0, target - latest.Net));
                }).ToArray() : []);
        })).ToArray();
        var tasks = active is null ? [] : await db.StudyPlanTasks.AsNoTracking().Where(x => x.StudentId == student && x.RevisionId == active.Id)
            .OrderBy(x => x.PlannedDate).ThenBy(x => x.PlannedStartMinute).ThenBy(x => x.Id).Take(10001).ToArrayAsync(ct);
        if (tasks.Length > 10000) throw new BusinessRuleException("StudyPlanning.ReportLimit", "Plan rapor sınırını aşıyor.");
        var overdue = tasks.Where(x => !x.IsCompleted && x.PlannedDate < today).ToArray();
        var windows = availability?.TimeZoneId == zoneId ? availability.Windows.ToArray()
            : weekly?.TimeZoneId == zoneId ? weekly.Slots.Select(x => new StudyAvailabilityWindow(x.Day, x.StartMinute, x.StartMinute + x.DurationMinutes)).ToArray() : [];
        var merged = new List<StudyAvailabilityWindow>();
        foreach (var window in windows.OrderBy(x => x.Day).ThenBy(x => x.StartMinute))
        {
            if (merged.LastOrDefault() is { } last && last.Day == window.Day && last.EndMinute >= window.StartMinute)
                merged[^1] = new(last.Day, last.StartMinute, Math.Max(last.EndMinute, window.EndMinute));
            else merged.Add(window);
        }
        var recovery = SuggestRecovery(active, tasks, merged.ToArray(), today);
        var reason = active is null ? "NoActivePlan" : overdue.Length == 0 ? "NoOverdueWork" : windows.Length == 0 ? "NoStudyHours"
            : recovery.Length == 0 ? "NoFreeSlot" : "ReviewBeforeApplying";
        var inPeriod = tasks.Where(x => x.PlannedDate >= from && x.PlannedDate <= to).ToArray();
        await transaction.CommitAsync(ct);
        return new(from, to, today, zoneId, trends, targets, inPeriod.Length, inPeriod.Count(x => x.IsCompleted), overdue.Length,
            overdue.Count(x => x.IsPinned), recovery, reason);
    }

    private static RecoveryProposal[] SuggestRecovery(StudyPlanRevision? plan, StudyPlanTask[] tasks,
        StudyAvailabilityWindow[] windows, DateOnly today)
    {
        if (plan is null) return [];
        var proposals = new List<RecoveryProposal>();
        var blocks = new List<List<StudyPlanTask>>();
        foreach (var task in tasks.Where(x => !x.IsCompleted && !x.IsPinned && x.PlannedDate < today && x.PlannedStartMinute.HasValue))
        {
            var previous = blocks.LastOrDefault()?.LastOrDefault();
            if (previous is not null && previous.PlannedDate == task.PlannedDate
                && previous.PlannedStartMinute + previous.PlannedMinutes == task.PlannedStartMinute
                && previous.WeeklyLessonId == task.WeeklyLessonId
                && (previous.LessonNameSnapshot ?? previous.Title) == (task.LessonNameSnapshot ?? task.Title)) blocks[^1].Add(task);
            else blocks.Add([task]);
        }
        // Work stays in the existing plan horizon. Do not extend a June deadline implicitly.
        var lastDay = plan.WeeklySource?.EndDate ?? today.AddDays(14);
        if (lastDay > today.AddDays(14)) lastDay = today.AddDays(14);
        foreach (var block in blocks)
        {
            if (proposals.Count == 10) break;
            var task = block[0]; var minutes = block.Sum(x=>x.PlannedMinutes);
            var found = false;
            for (var day = today.AddDays(1); day <= lastDay && !found; day = day.AddDays(1))
            {
                // A day with untimed work cannot be proven free.
                if (tasks.Any(x => x.PlannedDate == day && !x.PlannedStartMinute.HasValue)) continue;
                foreach (var window in windows.Where(x => x.Day == day.DayOfWeek).OrderBy(x => x.StartMinute))
                {
                    for (var minute = (window.StartMinute + 29) / 30 * 30; minute + minutes <= window.EndMinute; minute += 30)
                    {
                        if (tasks.Any(x => x.PlannedDate == day && x.PlannedStartMinute < minute + minutes && x.PlannedStartMinute + x.PlannedMinutes > minute)
                            || proposals.Any(x => x.PlannedDate == day && x.StartMinute < minute + minutes && x.StartMinute + x.Minutes > minute)) continue;
                        proposals.Add(new(plan.Id, plan.Version, block.Select(x=>x.Id).ToArray(), task.LessonNameSnapshot ?? task.Title,
                            task.PlannedDate, task.PlannedStartMinute!.Value, day, minute, minutes));
                        found = true; break;
                    }
                    if (found) break;
                }
            }
        }
        return proposals.ToArray();
    }
}
