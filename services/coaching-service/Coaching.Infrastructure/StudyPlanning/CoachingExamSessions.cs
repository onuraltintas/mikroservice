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
public static class CoachingExamSessions
{
    /// <summary>Source: Student, Session, Catalog or Estimated (G2: a year-only target; the date is a guess, shown as "tahmini").</summary>
    public sealed record Target(string Name, DateOnly Date, string Source)
    {
        /// <summary>G2 review: an estimate with no catalog session behind it (15 June); the plan does not use it as its end.</summary>
        public bool Guessed { get; init; }
    }

    /// <summary>
    /// G2 review (L4): the day an estimate is carried from: the earliest session of the latest year the catalog knows (YDS and
    /// YÖKDİL have several a year; the first one of the year is the nearest guess). Null when the exam never had a session.
    /// </summary>
    public static DateOnly? EstimateReference(IEnumerable<DateOnly> sessionDates)
    {
        var dates = sessionDates.ToArray();
        if (dates.Length == 0) return null;
        var year = dates.Max(x => x.Year);
        return dates.Where(x => x.Year == year).Min();
    }

    /// <summary>
    /// G2 review (M1): the first year whose exam has not happened yet: a year with sessions counts while one of them is upcoming,
    /// a year without one while its estimated date is. In July 2027, with the 2027 session passed, this is 2028.
    /// </summary>
    public static int FirstOpenYear(IReadOnlyCollection<DateOnly> sessionDates, DateOnly today)
    {
        var reference = EstimateReference(sessionDates);
        for (var year = today.Year; ; year++)
        {
            var inYear = sessionDates.Where(x => x.Year == year).ToArray();
            if (inYear.Length > 0 ? inYear.Any(x => x > today) : EstimatedDate(reference, year) > today) return year;
        }
    }

    /// <summary>G2 review (M1): <see cref="FirstOpenYear"/> of each exam, from its active sessions.</summary>
    public static async Task<IReadOnlyDictionary<string, int>> FirstOpenYearsAsync(CoachingDbContext db, IReadOnlyCollection<string> examCodes, DateOnly today,
        CancellationToken ct)
    {
        var codes = examCodes.ToArray();
        var dates = await (from s in db.Set<StudyCatalogExamSession>().AsNoTracking()
                           join e in db.Set<StudyCatalogExam>().AsNoTracking() on s.ExamId equals e.Id
                           where e.IsActive && Enumerable.Contains(codes, e.Code)
                           select new { e.Code, s.Date }).ToListAsync(ct);
        return codes.Distinct().ToDictionary(x => x, x => FirstOpenYear(dates.Where(d => d.Code == x).Select(d => d.Date).ToArray(), today));
    }

    /// <summary>
    /// G2: the guess for a year with no catalog session: the month and day of <see cref="EstimateReference"/> carried to that year
    /// (29 February becomes 28 February in other years), else 15 June.
    /// </summary>
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
        // G2 review (L5): only an active exam's sessions count, as everywhere else.
        var sessions = await (from s in db.Set<StudyCatalogExamSession>().AsNoTracking()
                              join e in db.Set<StudyCatalogExam>().AsNoTracking() on s.ExamId equals e.Id
                              where e.Code == examCode && e.IsActive
                              select new { e.Name, s.Date }).ToListAsync(ct);
        var inYear = sessions.Where(x => x.Date.Year == year && x.Date > today).OrderBy(x => x.Date).FirstOrDefault();
        if (inYear is not null) return new(inYear.Name, inYear.Date, "Catalog");
        var reference = EstimateReference(sessions.Select(x => x.Date));
        var name = sessions.FirstOrDefault()?.Name
            ?? await db.Set<StudyCatalogExam>().AsNoTracking().Where(x => x.Code == examCode && x.IsActive).Select(x => x.Name).FirstOrDefaultAsync(ct)
            ?? (examCode == "TYT" ? "YKS" : examCode);
        var estimate = EstimatedDate(reference, year);
        return estimate > today ? new(name, estimate, "Estimated") { Guessed = reference is null } : null;
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
