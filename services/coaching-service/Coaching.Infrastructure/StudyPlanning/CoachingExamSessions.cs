using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>
/// KM1 (01.11.2026): the exam date a student works towards. In order: the student's own date (roadmap O7), the session they
/// chose (YDS and YÖKDİL have several a year), the year they chose when the catalog has no session for it yet (G2 #40: an
/// estimated date), else the nearest upcoming session of their plan's active exams. With one session per exam (LGS, YKS) this
/// is the old single catalog date.
/// </summary>
internal static class CoachingExamSessions
{
    /// <summary>Source: Student, Session, Catalog or Estimated (G2: a year-only target; the date is a guess, shown as "tahmini").</summary>
    public sealed record Target(string Name, DateOnly Date, string Source);

    /// <summary>G2: the guess for a year with no catalog session: the month and day of the exam's last known session, else 15 June.</summary>
    public static DateOnly EstimatedDate(DateOnly? lastKnown, int year) => lastKnown is { } known
        ? new DateOnly(year, known.Month, Math.Min(known.Day, DateTime.DaysInMonth(year, known.Month)))
        : new DateOnly(year, 6, 15);

    /// <summary>
    /// The student's target exam: the own date, the chosen session, the chosen year (G2), else the nearest catalog session of
    /// <paramref name="fallbackCodes"/>. A year-only target never falls back to an earlier year's session (a 2028 student is not
    /// counted down to the 2027 exam). <paramref name="includeOwnDate"/> false gives the date the student's own date replaces.
    /// </summary>
    public static async Task<Target?> ForStudentAsync(CoachingDbContext db, Guid student, IReadOnlyCollection<string> fallbackCodes, DateOnly today,
        CancellationToken ct, bool includeOwnDate = true)
    {
        var own = await db.StudentLearningContexts.AsNoTracking().Where(x => x.StudentUserId == student)
            .Select(x => new { x.TargetExamDate, x.TargetExamSessionId, x.TargetExamYear, x.Purpose }).SingleOrDefaultAsync(ct);
        if (includeOwnDate && own?.TargetExamDate is { } date && date > today) return new("Sınavın", date, "Student");
        var chosen = await ChosenAsync(db, own?.TargetExamSessionId, today, ct);
        if (chosen is not null) return chosen;
        int? minYear = null;
        if (own is { TargetExamSessionId: null, TargetExamYear: { } year } && LearningPurposes.CatalogExamCode(own.Purpose) is { } code)
        {
            var estimated = await ForYearAsync(db, code, year, today, ct);
            if (estimated is not null) return estimated;
            minYear = year;
        }
        return await NextCatalogAsync(db, fallbackCodes, today, ct, minYear);
    }

    public static async Task<Target?> ChosenAsync(CoachingDbContext db, Guid? sessionId, DateOnly today, CancellationToken ct)
    {
        if (sessionId is not { } id) return null;
        var chosen = await (from s in db.Set<StudyCatalogExamSession>().AsNoTracking()
                            join e in db.Set<StudyCatalogExam>().AsNoTracking() on s.ExamId equals e.Id
                            where s.Id == id && e.IsActive && s.Date > today
                            select new { e.Name, s.Label, s.Date }).SingleOrDefaultAsync(ct);
        return chosen is null ? null : new($"{chosen.Name} · {chosen.Label}", chosen.Date, "Session");
    }

    /// <summary>
    /// G2 (#40): a year-only target. The first upcoming session of the exam in that year once the catalog has one; else the
    /// estimated date (<see cref="EstimatedDate"/>). Null when that date has passed.
    /// </summary>
    public static async Task<Target?> ForYearAsync(CoachingDbContext db, string examCode, int year, DateOnly today, CancellationToken ct)
    {
        var sessions = await (from s in db.Set<StudyCatalogExamSession>().AsNoTracking()
                              join e in db.Set<StudyCatalogExam>().AsNoTracking() on s.ExamId equals e.Id
                              where e.Code == examCode
                              select new { e.Name, e.IsActive, s.Date }).ToListAsync(ct);
        var inYear = sessions.Where(x => x.IsActive && x.Date.Year == year && x.Date > today).OrderBy(x => x.Date).FirstOrDefault();
        if (inYear is not null) return new(inYear.Name, inYear.Date, "Catalog");
        var last = sessions.OrderByDescending(x => x.Date).FirstOrDefault();
        var name = last?.Name ?? await db.Set<StudyCatalogExam>().AsNoTracking().Where(x => x.Code == examCode).Select(x => x.Name).FirstOrDefaultAsync(ct)
            ?? (examCode == "TYT" ? "YKS" : examCode);
        var estimate = EstimatedDate(last?.Date, year);
        return estimate > today ? new(name, estimate, "Estimated") : null;
    }

    public static async Task<Target?> NextCatalogAsync(CoachingDbContext db, IReadOnlyCollection<string> examCodes, DateOnly today, CancellationToken ct,
        int? minYear = null)
    {
        if (examCodes.Count == 0) return null;
        var codes = examCodes.ToArray();
        var after = minYear is { } year && year > today.Year ? new DateOnly(year, 1, 1).AddDays(-1) : today;
        var next = await (from s in db.Set<StudyCatalogExamSession>().AsNoTracking()
                          join e in db.Set<StudyCatalogExam>().AsNoTracking() on s.ExamId equals e.Id
                          where Enumerable.Contains(codes, e.Code) && e.IsActive && s.Date > after
                          orderby s.Date, e.Code
                          select new { e.Name, s.Date }).FirstOrDefaultAsync(ct);
        return next is null ? null : new(next.Name, next.Date, "Catalog");
    }
}
