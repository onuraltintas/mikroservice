using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace Coaching.Infrastructure.StudyPlanning;
internal static class CoachingWeeklyResultReader
{
    internal static async Task<WeeklyResultSnapshot> ReadAsync(CoachingDbContext db,TimeProvider clock,Guid student,DateOnly week,CancellationToken ct)
        => (await ReadManyAsync(db,clock,student,[week],ct))[week];

    // The teacher queue evaluates many historical weeks. Load shared work/decisions once per student.
    internal static async Task<Dictionary<DateOnly,WeeklyResultSnapshot>> ReadManyAsync(CoachingDbContext db,TimeProvider clock,Guid student,IReadOnlyList<DateOnly> weeks,CancellationToken ct)
    {
        if(weeks.Count==0)return [];
        var first=weeks.Min();var last=weeks.Max();var end=last.AddDays(6);
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var fromUtc=TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue),zone);
        var untilUtc=TimeZoneInfo.ConvertTimeToUtc(last.AddDays(7).ToDateTime(TimeOnly.MinValue),zone);
        var previous=first.AddDays(-7);
        var decisions=await db.Set<WeeklyDecision>().AsNoTracking().Where(x=>x.StudentId==student&&x.ReviewWeekStart<last
            && (x.ReviewWeekStart>=previous || x.Status=="Planned"||x.Status=="InProgress"
                ||x.DueOn>=first&&x.DueOn<=end||x.CompletedAt>=fromUtc&&x.CompletedAt<untilUtc
                ||x.Status=="Cancelled"&&x.UpdatedAt>=fromUtc&&x.UpdatedAt<untilUtc))
            .OrderBy(x=>x.ReviewWeekStart).ThenBy(x=>x.DueOn).ThenBy(x=>x.Id).Take(10001).ToArrayAsync(ct);
        if(decisions.Length>10000)throw new ArgumentException("Karar sonuçları inceleme sınırını aşıyor.");
        var tasks=await CoachingStudyWorkReports.ReadAsync(db,student,first,end,ct);
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),zone).DateTime);
        var views=(await CoachingWeeklyWorkReader.EnrichAsync(db,clock,decisions.Select(x=>CoachingWeeklyWorkReader.View(x,false,today,false)).ToArray(),student,ct)).ToDictionary(x=>x.Id);
        var linkedIds=decisions.Where(x=>x.StudyTaskId.HasValue).Select(x=>x.StudyTaskId!.Value)
            .Concat(views.Values.Where(x=>x.Work?.Study is not null).Select(x=>x.Work!.Study!.Id)).Distinct().ToArray();
        var roots=await db.StudyPlanTasks.AsNoTracking().Where(x=>x.StudentId==student&&Enumerable.Contains(linkedIds,x.Id))
            .ToDictionaryAsync(x=>x.Id,x=>x.OriginalTaskId??x.Id,ct);
        var output=new Dictionary<DateOnly,WeeklyResultSnapshot>();
        foreach(var week in weeks.Distinct()) {
            var weekEnd=week.AddDays(6);var previousWeek=week.AddDays(-7);
            var weekFrom=TimeZoneInfo.ConvertTimeToUtc(week.ToDateTime(TimeOnly.MinValue),zone);
            var weekUntil=TimeZoneInfo.ConvertTimeToUtc(week.AddDays(7).ToDateTime(TimeOnly.MinValue),zone);
            var selected=decisions.Where(x=>x.ReviewWeekStart<week&&(x.ReviewWeekStart==previousWeek||x.Status is "Planned" or "InProgress"
                ||x.DueOn>=week&&x.DueOn<=weekEnd||x.CompletedAt>=weekFrom&&x.CompletedAt<weekUntil
                ||x.Status=="Cancelled"&&x.UpdatedAt>=weekFrom&&x.UpdatedAt<weekUntil)).ToArray();
            if(selected.Length>500)throw new ArgumentException("Bu hafta çok fazla karar sonucu içeriyor; inceleme sınırı 500 karardır.");
            var work=tasks.Where(x=>x.Task.PlannedDate>=week&&x.Task.PlannedDate<=weekEnd||x.Task.WorkFromDate<=weekEnd&&x.Task.WorkToDate>=week).ToArray();
            var summary=CoachingStudyWorkReports.Calculate(week,weekEnd,work);
            var studies=work.Select(x=> {
                var days=x.Task.WorkDays.Where(d=>d.WorkedOn>=week&&d.WorkedOn<=weekEnd).ToArray();
                return new WeeklyResultStudy(x.Task.Id,x.Task.OriginalTaskId??x.Task.Id,x.Task.RevisionId,x.Task.Version,x.Task.PlannedDate,x.Task.Title,x.Task.PlannedMinutes,
                    days.Length==0?null:days.Sum(d=>d.Minutes),days.Any(d=>d.Questions.HasValue)?days.Sum(d=>d.Questions??0):null,x.Task.IsCompleted,x.Task.CompletedAt,days,CoachingReviewFollowUp.StudyRevision(x.Task,week,weekEnd));
            }).ToArray();
            var results=selected.Select(d=> {
                var x=views[d.Id];Guid? root=x.Work?.Study is {} study&&roots.TryGetValue(study.Id,out var id)?id:null;
                return new WeeklyResultDecision(x.Id,x.Version,x.ReviewWeekStart,x.Title,x.Status,x.DueOn,x.CompletedAt,x.Work,root,studies.Where(t=>t.RootTaskId==root).ToArray());
            }).ToArray();
            var workSummary=new WeeklyWorkSummary(summary.ScheduledTasks,summary.CompletedTasks,summary.PlannedMinutes,summary.ActualMinutes,summary.LegacyDateActualMinutes);
            var fingerprint=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {Week=week,Summary=workSummary,Decisions=results,Studies=studies})));
            output[week]=new(fingerprint,clock.GetUtcNow().UtcDateTime,workSummary,results,studies);
        }
        return output;
    }
}
