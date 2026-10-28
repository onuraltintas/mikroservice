using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;
public static class CoachingReflectionBacklog
{
    public sealed record Missing(Guid StudentId,DateOnly WeekStart);
    // This is a durable gap query: submitting a reflection removes only that student's week.
    public static async Task<Missing[]> MissingAsync(CoachingDbContext db,Guid[] students,DateOnly today,DateOnly through,CancellationToken ct)
    {
        if(students.Length==0)return [];
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var currentWeek=today.AddDays(-((int)today.DayOfWeek+6)%7);
        var lastEnd=through<currentWeek.AddDays(-1)?through:currentWeek.AddDays(-1);
        var profiles=await db.CoachingStudentProfiles.AsNoTracking().Where(x=>Enumerable.Contains(students,x.UserId)&&x.IsActive)
            .Select(x=>new {x.UserId,x.CreatedAt}).ToArrayAsync(ct);
        var starts=await db.CoachingTeacherStudentAssignments.AsNoTracking().Where(x=>Enumerable.Contains(students,x.StudentUserId))
            .GroupBy(x=>x.StudentUserId).Select(g=>new {StudentId=g.Key,Start=g.Min(x=>x.StartDate)}).ToDictionaryAsync(x=>x.StudentId,x=>x.Start,ct);
        var firstWeeks=profiles.Select(profile=> {
            var start=starts.TryGetValue(profile.UserId,out var assigned)&&assigned<profile.CreatedAt?assigned:profile.CreatedAt;
            var day=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(start,DateTimeKind.Utc),zone));
            return new {profile.UserId,Week=day.AddDays(-((int)day.DayOfWeek+6)%7)};
        }).ToArray();
        // Roadmap D1: gaps are counted from the school year (the week of 1 September), not from enrollment.
        var schoolYear=SchoolYearWeek(today);
        firstWeeks=firstWeeks.Select(x=>x.Week<schoolYear?new {x.UserId,Week=schoolYear}:x).ToArray();
        if(firstWeeks.Length==0||firstWeeks.Min(x=>x.Week)>lastEnd.AddDays(-6))return [];
        var firstWeek=firstWeeks.Min(x=>x.Week);
        var saved=await db.Set<WeeklyReflection>().AsNoTracking().Where(x=>Enumerable.Contains(students,x.StudentId)&&x.WeekStart>=firstWeek&&x.WeekStart<=lastEnd)
            .Select(x=>new {x.StudentId,x.WeekStart}).ToArrayAsync(ct);
        var keys=saved.Select(x=>(x.StudentId,x.WeekStart)).ToHashSet();var result=new List<Missing>();
        foreach(var profile in firstWeeks) {
            for(var week=profile.Week;week<=lastEnd.AddDays(-6);week=week.AddDays(7)) {
                if(!keys.Contains((profile.UserId,week)))result.Add(new(profile.UserId,week));
                // Callers refuse more than 10.000 rows with a clear message; one extra row is enough to tell.
                if(result.Count>10000)return result.ToArray();
            }
        }
        return result.OrderBy(x=>x.WeekStart).ThenBy(x=>x.StudentId).ToArray();
    }
    /// <summary>Monday of the week holding 1 September of the school year that <paramref name="today"/> belongs to.</summary>
    public static DateOnly SchoolYearWeek(DateOnly today)
    {
        var first=new DateOnly(today.Month>=9?today.Year:today.Year-1,9,1);
        return first.AddDays(-(((int)first.DayOfWeek+6)%7));
    }
}
