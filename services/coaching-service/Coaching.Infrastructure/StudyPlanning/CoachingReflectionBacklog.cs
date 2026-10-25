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
        if(firstWeeks.Length==0||firstWeeks.Min(x=>x.Week)>lastEnd.AddDays(-6))return [];
        var firstWeek=firstWeeks.Min(x=>x.Week);
        var saved=await db.Set<WeeklyReflection>().AsNoTracking().Where(x=>Enumerable.Contains(students,x.StudentId)&&x.WeekStart>=firstWeek&&x.WeekStart<=lastEnd)
            .Select(x=>new {x.StudentId,x.WeekStart}).ToArrayAsync(ct);
        var keys=saved.Select(x=>(x.StudentId,x.WeekStart)).ToHashSet();var result=new List<Missing>();
        foreach(var profile in firstWeeks) {
            for(var week=profile.Week;week<=lastEnd.AddDays(-6);week=week.AddDays(7)) {
                if(!keys.Contains((profile.UserId,week)))result.Add(new(profile.UserId,week));
                if(result.Count>10000)throw new ArgumentException("Eksik değerlendirmeler takip sınırını aşıyor. Daha erken bir bitiş tarihi veya daha dar öğrenci kapsamı seçin.");
            }
        }
        return result.OrderBy(x=>x.WeekStart).ThenBy(x=>x.StudentId).ToArray();
    }
}
