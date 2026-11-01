using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>
/// KM1 (01.11.2026): the exam date a student works towards. In order: the student's own date (roadmap O7), the session they
/// chose (YDS and YÖKDİL have several a year), else the nearest upcoming session of their plan's active exams. With one
/// session per exam (LGS, YKS) this is the old single catalog date.
/// </summary>
internal static class CoachingExamSessions
{
    public sealed record Target(string Name, DateOnly Date, string Source);

    public static async Task<Target?> ChosenAsync(CoachingDbContext db, Guid? sessionId, DateOnly today, CancellationToken ct)
    {
        if (sessionId is not { } id) return null;
        var chosen = await (from s in db.Set<StudyCatalogExamSession>().AsNoTracking()
                            join e in db.Set<StudyCatalogExam>().AsNoTracking() on s.ExamId equals e.Id
                            where s.Id == id && e.IsActive && s.Date > today
                            select new { e.Name, s.Label, s.Date }).SingleOrDefaultAsync(ct);
        return chosen is null ? null : new($"{chosen.Name} · {chosen.Label}", chosen.Date, "Session");
    }

    public static async Task<Target?> NextCatalogAsync(CoachingDbContext db, IReadOnlyCollection<string> examCodes, DateOnly today, CancellationToken ct)
    {
        if (examCodes.Count == 0) return null;
        var codes = examCodes.ToArray();
        var next = await (from s in db.Set<StudyCatalogExamSession>().AsNoTracking()
                          join e in db.Set<StudyCatalogExam>().AsNoTracking() on s.ExamId equals e.Id
                          where Enumerable.Contains(codes, e.Code) && e.IsActive && s.Date > today
                          orderby s.Date, e.Code
                          select new { e.Name, s.Date }).FirstOrDefaultAsync(ct);
        return next is null ? null : new(next.Name, next.Date, "Catalog");
    }
}
