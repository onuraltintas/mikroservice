using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>
/// Roadmap D2: the time zone a student's "today" is counted in: the zone of the active weekly plan, else the student's
/// study-hours setting, else the weekly lesson draft, else Türkiye. K4 (31.10.2026): every student-facing "today" (Bugün,
/// weekly review and home, practice progress) uses this one rule.
/// </summary>
public static class CoachingStudentZone
{
    public const string Default = "Europe/Istanbul";

    public static async Task<string> IdAsync(CoachingDbContext db, Guid student, CancellationToken ct)
    {
        var source = await db.StudyPlanRevisions.AsNoTracking().Where(x => x.StudentId == student && x.Status == StudyPlanStatus.Active)
            .Select(x => x.WeeklySource).FirstOrDefaultAsync(ct);
        var planZone = source?.TimeZoneId;
        var zone = planZone ?? await db.StudyAvailability.AsNoTracking().Where(x => x.StudentId == student)
            .Select(x => x.TimeZoneId).FirstOrDefaultAsync(ct)
            ?? await db.Set<WeeklyStudyDraft>().AsNoTracking().Where(x => x.StudentId == student)
            .Select(x => x.TimeZoneId).FirstOrDefaultAsync(ct);
        return Usable(zone);
    }

    public static TimeZoneInfo Find(string? zoneId) => TimeZoneInfo.FindSystemTimeZoneById(Usable(zoneId));

    private static string Usable(string? zoneId) =>
        !string.IsNullOrWhiteSpace(zoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out _) ? zoneId : Default;
}
