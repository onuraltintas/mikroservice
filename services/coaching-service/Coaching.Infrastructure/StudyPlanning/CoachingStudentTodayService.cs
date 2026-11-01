using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed record StudentTodayExam(string Name, DateOnly Date, int DaysLeft, string Source);
public sealed record StudentTodayStreak(int CurrentStreak, int LongestStreak, bool ActiveToday);
public sealed record StudentTodayWeek(DateOnly WeekStart, int Planned, int Completed);
public sealed record StudentTodayGoal(Guid GoalId, string Title, string ExamCode, decimal TargetNet, decimal LatestNet, decimal RemainingNet);
public sealed record StudentTodayReview(bool Needed, DateOnly WeekStart);
public sealed record StudentTodaySession(Guid Id, string Title, DateTime ScheduledDate, int DurationMinutes, string? MeetingLink);
public sealed record StudentTodayFacts(DateOnly Today, string TimeZoneId, StudentTodayExam? Exam, StudentTodayStreak Streak,
    StudentTodayWeek Week, StudentTodayGoal? Goal, StudentTodayReview WeeklyReview, StudentTodaySession? NextSession, bool HasActivePlan);

/// <summary>
/// T3 (31.10.2026): everything the student's "Bugün" screen shows besides the agenda, read for the student's own day:
/// the exam countdown (the student's own exam date, else the nearest catalog date of the exams in their weekly draft),
/// the streak (the D5 rules), this week's plan tasks (Monday to Sunday), the gap to the primary goal's net target
/// (latest past practice exam of that type), whether last week's reflection is due, and the next session.
/// Read-only; the caller has already checked that the current user is this student.
/// </summary>
public sealed class CoachingStudentTodayService(CoachingDbContext db, CoachingMotivationService motivation, TimeProvider clock)
{
    public async Task<StudentTodayFacts> ReadAsync(Guid student, CancellationToken ct)
    {
        var zoneId = await CoachingStudentZone.IdAsync(db, student, ct);
        var zone = CoachingStudentZone.Find(zoneId);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        var plan = await db.StudyPlanRevisions.AsNoTracking().Where(x => x.StudentId == student && x.Status == StudyPlanStatus.Active)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var week = new StudentTodayWeek(weekStart, 0, 0);
        if (plan is { } planId)
        {
            var counts = await db.StudyPlanTasks.AsNoTracking()
                .Where(x => x.StudentId == student && x.RevisionId == planId && x.SkippedAt == null
                    && x.PlannedDate >= weekStart && x.PlannedDate <= weekStart.AddDays(6))
                .GroupBy(x => x.IsCompleted).Select(g => new { Done = g.Key, Count = g.Count() }).ToArrayAsync(ct);
            week = new(weekStart, counts.Sum(x => x.Count), counts.Where(x => x.Done).Sum(x => x.Count));
        }

        var streak = await motivation.GetAsync(student, student, ct);
        var missing = await CoachingReflectionBacklog.MissingAsync(db, [student], today, today, ct);
        var reviewWeek = weekStart.AddDays(-7);
        if (missing.Length > 0)
            reviewWeek = await db.Set<WeeklyReflection>().AsNoTracking().AnyAsync(x => x.StudentId == student, ct) ? missing[0].WeekStart : missing[^1].WeekStart;

        var session = await db.CoachingSessions.AsNoTracking()
            .Where(x => x.Status == SessionStatus.Scheduled && x.ScheduledDate > now.UtcDateTime && x.Attendances.Any(a => a.StudentId == student))
            .OrderBy(x => x.ScheduledDate).ThenBy(x => x.Id)
            .Select(x => new StudentTodaySession(x.Id, x.Title, x.ScheduledDate, x.DurationMinutes, x.MeetingLink)).FirstOrDefaultAsync(ct);

        return new(today, zoneId, await ExamAsync(student, today, ct),
            new(streak.CurrentStreak, streak.LongestStreak, streak.ActiveToday), week,
            await GoalAsync(student, now.UtcDateTime, ct), new(missing.Length > 0, reviewWeek), session, plan.HasValue);
    }

    private async Task<StudentTodayExam?> ExamAsync(Guid student, DateOnly today, CancellationToken ct)
    {
        var own = await db.StudentLearningContexts.AsNoTracking().Where(x => x.StudentUserId == student)
            .Select(x => x.TargetExamDate).SingleOrDefaultAsync(ct);
        if (own is { } date && date > today) return new("Sınavın", date, date.DayNumber - today.DayNumber, "Student");
        var draft = await db.Set<WeeklyStudyDraft>().AsNoTracking().Where(x => x.StudentId == student).SingleOrDefaultAsync(ct);
        var codes = draft?.Slots.Select(x => x.ExamCode).Where(x => x is not null).Distinct().ToArray() ?? [];
        if (codes.Length == 0) return null;
        var exam = await db.Set<StudyCatalogExam>().AsNoTracking()
            .Where(x => Enumerable.Contains(codes, x.Code) && x.IsActive && x.ExamDate > today)
            .OrderBy(x => x.ExamDate).ThenBy(x => x.Code).FirstOrDefaultAsync(ct);
        return exam?.ExamDate is { } catalog ? new(exam.Name, catalog, catalog.DayNumber - today.DayNumber, "Catalog") : null;
    }

    private async Task<StudentTodayGoal?> GoalAsync(Guid student, DateTime now, CancellationToken ct)
    {
        var primary = await db.Set<StudentPrimaryGoal>().AsNoTracking().Where(x => x.StudentId == student)
            .Select(x => (Guid?)x.GoalId).SingleOrDefaultAsync(ct);
        var goals = await db.AcademicGoals.AsNoTracking().Where(x => x.StudentId == student && !x.IsCompleted)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(50).ToArrayAsync(ct);
        var ordered = goals.OrderByDescending(x => x.Id == primary).ToArray();
        var targets = ordered.SelectMany(goal => goal.NetTargets
            .Select(t => (Goal: goal, Code: t.ExamCode, Target: t.TargetNet))).ToArray();
        if (targets.Length == 0) return null;
        var recent = await db.ExamResults.AsNoTracking().Include(x => x.Exam)
            .Where(x => x.StudentId == student && x.PracticeExamJson != null && x.Exam.ExamDate <= now)
            .OrderByDescending(x => x.Exam.ExamDate).ThenByDescending(x => x.CreatedAt).Take(60).ToArrayAsync(ct);
        foreach (var target in targets)
        {
            var latest = recent.Select(x => x.GetPracticeExam()).FirstOrDefault(x => x?.ExamCode == target.Code);
            if (latest is null) continue;
            var net = PracticeExamRules.Summarize(latest).Net;
            return new(target.Goal.Id, target.Goal.Title, target.Code, target.Target, net, Math.Max(0, target.Target - net));
        }
        return null;
    }
}
