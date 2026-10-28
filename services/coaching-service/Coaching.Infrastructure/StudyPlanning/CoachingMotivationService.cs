using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed record MotivationBadge(string Code, string Label, string Description, DateOnly? EarnedOn);

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
        var badges = new List<MotivationBadge>
        {
            new("first-day", "İlk adım", "İlk çalışma gününü tamamladın.", days.Length > 0 ? days[0] : null)
        };
        badges.AddRange(StreakBadges.Select(x => new MotivationBadge(x.Code, x.Label, x.Description,
            streakEarned.TryGetValue(x.Code, out var on) ? on : null)));
        badges.Add(new("first-submission", "İlk teslim", "İlk ödevini teslim ettin.", submissions.Length > 0 ? submissions[0] : null));
        badges.Add(new("ten-submissions", "10 teslim", "On ödev teslim ettin.", submissions.Length >= 10 ? submissions[9] : null));
        badges.Add(new("goal-reached", "Hedefe ulaştın", "Bir hedefini tamamladın.", firstGoal is null ? null : Day(firstGoal.CompletedAt)));

        var recent = goals.Where(x => x.CompletedAt > now.AddDays(-CelebrationDays) && x.CompletedAt <= now)
            .OrderByDescending(x => x.CompletedAt).FirstOrDefault();
        return new(student, current, longest, activeToday, days.Length > 0 ? days[^1] : null, zoneId, badges,
            recent is null ? null : new MotivationCelebration(recent.Id, recent.Title, DateTime.SpecifyKind(recent.CompletedAt, DateTimeKind.Utc)));
    }
}
