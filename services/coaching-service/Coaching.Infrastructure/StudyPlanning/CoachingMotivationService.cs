using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>T6 (31.10.2026): <see cref="Progress"/> of <see cref="Target"/> shows how close an unearned badge is ("5 deneme: 3/5").</summary>
public sealed record MotivationBadge(string Code, string Label, string Description, DateOnly? EarnedOn, int Progress = 0, int Target = 1);

public sealed record MotivationCelebration(Guid GoalId, string GoalTitle, DateTime CompletedAt);

public sealed record StudentMotivation(Guid StudentId, int CurrentStreak, int LongestStreak, bool ActiveToday,
    DateOnly? LastActiveDay, string TimeZoneId, IReadOnlyList<MotivationBadge> Badges, MotivationCelebration? Celebration);

/// <summary>
/// Roadmap D5: a study day is a day (in the student's own time zone) with at least one completed plan task or a
/// submitted assignment. The current streak is still alive when the student was active yesterday but not yet today.
/// Badges are worked out from the records each time (no extra table); a goal reached in the last 7 days is celebrated.
/// The student, linked parents and the student's teachers (the usual read authorization) see it.
/// </summary>
public sealed class CoachingMotivationService(CoachingDbContext db, ICoachingIdentityAuthorizationClient authorization, TimeProvider clock)
{
    private const int CelebrationDays = 7;
    private const int ExamBadgeTarget = 5;
    private static readonly (string Code, string Label, string Description, int Days)[] StreakBadges =
    [
        ("streak-3", "3 günlük seri", "Üç gün üst üste çalıştın.", 3),
        ("streak-7", "7 günlük seri", "Bir hafta boyunca her gün çalıştın.", 7),
        ("streak-30", "30 günlük seri", "Otuz gün üst üste çalıştın.", 30)
    ];

    public async Task<StudentMotivation> GetAsync(Guid viewer, Guid student, CancellationToken ct)
    {
        if (viewer == Guid.Empty || student == Guid.Empty
            || viewer != student && !(await authorization.AuthorizeStudentReadAsync(viewer, [student], ct)).Contains(student))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu öğrencinin çalışma serisini görme yetkiniz yok.");

        var zoneId = await CoachingStudentZone.IdAsync(db, student, ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        DateOnly Day(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));

        var completed = await db.StudyPlanTasks.AsNoTracking()
            .Where(x => x.StudentId == student && x.IsCompleted && x.CompletedAt != null)
            .Select(x => x.CompletedAt!.Value).ToListAsync(ct);
        var submitted = await db.AssignmentStudents.AsNoTracking()
            .Where(x => x.StudentId == student && x.SubmittedAt != null
                && (x.Status == StudentAssignmentStatus.Submitted || x.Status == StudentAssignmentStatus.Graded))
            .Select(x => x.SubmittedAt!.Value).ToListAsync(ct);
        var goals = await db.AcademicGoals.AsNoTracking()
            .Where(x => x.StudentId == student && x.IsCompleted && x.CompletedAt != null)
            .Select(x => new { x.Id, x.Title, CompletedAt = x.CompletedAt!.Value }).ToListAsync(ct);

        var days = completed.Concat(submitted).Select(Day).Distinct().Order().ToArray();
        var now = clock.GetUtcNow().UtcDateTime; var today = Day(now);

        // Runs of consecutive days; the day each streak badge was first reached.
        var longest = 0; var run = 0; DateOnly? previous = null;
        var streakEarned = new Dictionary<string, DateOnly>();
        foreach (var day in days)
        {
            run = previous is { } last && last.AddDays(1) == day ? run + 1 : 1;
            previous = day; longest = Math.Max(longest, run);
            foreach (var badge in StreakBadges)
                if (run >= badge.Days) streakEarned.TryAdd(badge.Code, day);
        }
        var activeToday = days.Length > 0 && days[^1] == today;
        var current = 0;
        if (days.Length > 0 && (days[^1] == today || days[^1] == today.AddDays(-1)))
        {
            current = 1;
            for (var i = days.Length - 1; i > 0 && days[i - 1].AddDays(1) == days[i]; i--) current++;
        }

        var submissions = submitted.Order().Select(Day).ToArray();
        var firstGoal = goals.OrderBy(x => x.CompletedAt).FirstOrDefault();
        // T6: practice exams of any source that already took place count towards the deneme badge.
        var exams = await (from result in db.ExamResults.AsNoTracking()
                           join exam in db.Exams.AsNoTracking() on result.ExamId equals exam.Id
                           where result.StudentId == student && exam.ExamDate <= now
                           orderby exam.ExamDate
                           select exam.ExamDate).Take(ExamBadgeTarget).ToListAsync(ct);
        var badges = new List<MotivationBadge>
        {
            new("first-day", "İlk adım", "İlk çalışma gününü tamamladın.", days.Length > 0 ? days[0] : null, Math.Min(days.Length, 1))
        };
        badges.AddRange(StreakBadges.Select(x => new MotivationBadge(x.Code, x.Label, x.Description,
            streakEarned.TryGetValue(x.Code, out var on) ? on : null, Math.Min(Math.Max(longest, current), x.Days), x.Days)));
        badges.Add(new("first-submission", "İlk teslim", "İlk ödevini teslim ettin.", submissions.Length > 0 ? submissions[0] : null, Math.Min(submissions.Length, 1)));
        badges.Add(new("ten-submissions", "10 teslim", "On ödev teslim ettin.", submissions.Length >= 10 ? submissions[9] : null, Math.Min(submissions.Length, 10), 10));
        badges.Add(new("five-exams", "5 deneme", "Beş deneme çözdün.", exams.Count >= ExamBadgeTarget ? Day(exams[ExamBadgeTarget - 1]) : null,
            exams.Count, ExamBadgeTarget));
        badges.Add(new("goal-reached", "Hedefe ulaştın", "Bir hedefini tamamladın.", firstGoal is null ? null : Day(firstGoal.CompletedAt), Math.Min(goals.Count, 1)));

        var recent = goals.Where(x => x.CompletedAt > now.AddDays(-CelebrationDays) && x.CompletedAt <= now)
            .OrderByDescending(x => x.CompletedAt).FirstOrDefault();
        return new(student, current, longest, activeToday, days.Length > 0 ? days[^1] : null, zoneId, badges,
            recent is null ? null : new MotivationCelebration(recent.Id, recent.Title, DateTime.SpecifyKind(recent.CompletedAt, DateTimeKind.Utc)));
    }
}
