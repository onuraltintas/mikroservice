using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

// A continuation keeps the complete curriculum allocation, but schedules only the declared work still needed.
// It does not copy actual effort and does not grant completed curriculum credit until the continuation is completed.
internal static class CoachingStudyContinuation
{
    internal sealed record Placement(StudyPlanTask Source, DateOnly Date, int? Start, int Minutes);
    internal static BusinessRuleException Conflict(string message) => new("StudyPlanning.Conflict",message);
    internal static int Minutes(StudyPlanTask task)
    {
        if (!task.IsCompleted && task.ActualMinutes.HasValue && !task.RemainingMinutes.HasValue)
            throw Conflict($"{task.Title}: kısmi çalışmanın kalan süresini mevcut planda belirtin; gerçekleşen süreden kazanım kapsamı tahmin edilmez.");
        return task.RemainingMinutes ?? task.PlannedMinutes;
    }
    internal static async Task<StudyPlanTask[]> SourcesAsync(CoachingDbContext db, Guid student, CancellationToken ct)
    {
        var active = await (from task in db.StudyPlanTasks.AsNoTracking() join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
            where task.StudentId == student && plan.StudentId == student && plan.IsActive select task).ToArrayAsync(ct);
        var linked = await (from decision in db.Set<WeeklyDecision>().AsNoTracking() join task in db.StudyPlanTasks.AsNoTracking() on decision.StudyTaskId equals (Guid?)task.Id
            join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
            where decision.StudentId == student && task.StudentId == student && plan.StudentId == student
                && (decision.Status == "Planned" || decision.Status == "InProgress")
                && (plan.Status == StudyPlanStatus.Active || plan.WasPublished) select task).ToArrayAsync(ct);
        var roots = linked.Where(x=>!x.IsCompleted).Select(x=>x.OriginalTaskId ?? x.Id).ToHashSet();
        var selected = active.Where(x=>!x.IsCompleted && (x.IsPinned || x.ActualMinutes.HasValue || x.RemainingMinutes.HasValue || x.PredecessorTaskId.HasValue || roots.Contains(x.OriginalTaskId ?? x.Id))).ToList();
        foreach(var task in linked.Where(x=>!x.IsCompleted))
            if(!active.Any(x=>(x.OriginalTaskId ?? x.Id)==(task.OriginalTaskId ?? task.Id)) && !selected.Any(x=>(x.OriginalTaskId ?? x.Id)==(task.OriginalTaskId ?? task.Id))) selected.Add(task);
        var legacyRoots=selected.Where(x=>x.OriginalTaskId.HasValue&&!x.PredecessorTaskId.HasValue&&!x.ActualMinutes.HasValue&&!x.RemainingMinutes.HasValue).Select(x=>x.OriginalTaskId!.Value).ToArray();
        if(legacyRoots.Length>0 && await db.StudyPlanTasks.AnyAsync(x=>x.StudentId==student&&!x.IsCompleted&&x.ActualMinutes.HasValue&&Enumerable.Contains(legacyRoots,x.OriginalTaskId ?? x.Id),ct))
            throw Conflict("Eski bir kısmi çalışmadan devreden görev var. Aktif görevde yalnız kalan kapsamı kaydedip yeniden önizleyin.");
        if(selected.GroupBy(x=>x.OriginalTaskId ?? x.Id).Any(x=>x.Count()>1)) throw Conflict("Aynı çalışmanın birden fazla aktif devamı var; bağlantıları inceleyin.");
        return selected.OrderByDescending(x=>x.IsPinned).ThenBy(x=>x.PlannedDate).ThenBy(x=>x.PlannedStartMinute).ThenBy(x=>x.Id).ToArray();
    }
    internal static Placement[] Place(StudyPlanTask[] sources, DateOnly start, DateOnly end, IReadOnlyList<(DayOfWeek Day,int Start,int End,Guid? Lesson)> windows, IReadOnlyList<Placement>? existing = null,
        (DateOnly Date,int Minute)? notBefore = null)
    {
        var merged=new List<(DayOfWeek Day,int Start,int End,Guid? Lesson)>();
        foreach(var group in windows.GroupBy(x=>(x.Day,x.Lesson)))
            foreach(var window in group.OrderBy(x=>x.Start)) {
                if(merged.Count>0 && merged[^1].Day==window.Day && merged[^1].Lesson==window.Lesson && merged[^1].End>=window.Start)
                    merged[^1]=(window.Day,merged[^1].Start,Math.Max(merged[^1].End,window.End),window.Lesson);
                else merged.Add(window);
            }
        windows=merged;
        var placed=existing?.ToList() ?? new List<Placement>();
        foreach(var source in sources)
        {
            var minutes=Minutes(source);
            bool Free(DateOnly date,int at)=>at+minutes<=1440 && !placed.Any(x=>x.Date==date&&x.Start<at+minutes&&x.Start+x.Minutes>at);
            var own=source.PlannedStartMinute;
            if(source.PlannedDate>=start&&source.PlannedDate<=end&&own.HasValue&&Free(source.PlannedDate,own.Value)
                && (source.IsPinned || windows.Any(w=>w.Day==source.PlannedDate.DayOfWeek&&(source.WeeklyLessonId is null||w.Lesson is null||w.Lesson==source.WeeklyLessonId)&&w.Start<=own&&w.End>=own+minutes)))
            {placed.Add(new(source,source.PlannedDate,own,minutes));continue;}
            // Live test 16.11.2026 (#28): the weekly layout may have changed since the work was planned. The work first looks for
            // its own lesson's hour, then for any study hour, and at last starts in an hour it does not fully fit; started work is
            // never refused only because no single lesson hour is long enough.
            Placement? Find(bool ownLesson,bool whole)
            {
                for(var day=start;day<=end;day=day.AddDays(1))
                    foreach(var window in windows.Where(w=>w.Day==day.DayOfWeek&&(!ownLesson||source.WeeklyLessonId is null||w.Lesson is null||w.Lesson==source.WeeklyLessonId)).OrderBy(w=>w.Start))
                    {
                        // A work moved to another hour of the plan's first day goes to an hour that has not passed yet.
                        var at=notBefore is {} limit&&limit.Date==day?Math.Max(window.Start,limit.Minute):window.Start;
                        foreach(var occupied in placed.Where(p=>p.Date==day&&p.Start.HasValue).OrderBy(p=>p.Start))
                            if(occupied.Start<at+minutes && occupied.Start+occupied.Minutes>at)at=occupied.Start.Value+occupied.Minutes;
                        if((whole?at+minutes<=window.End:at<window.End)&&Free(day,at))return new(source,day,at,minutes);
                    }
                return null;
            }
            var found=Find(true,true)??Find(false,true)??Find(true,false)??Find(false,false);
            if(found is null)throw Conflict($"{source.Title}: kalan {minutes} dakikalık devam çalışması için {start:dd.MM.yyyy}–{end:dd.MM.yyyy} arasında boş çalışma saati yok. Haftalık düzene saat ekleyin veya plan dönemini uzatın.");
            placed.Add(found);
        }
        return placed.Skip(existing?.Count ?? 0).ToArray();
    }
    /// <param name="carry">
    /// False for a waiting task that only keeps its place when a plan is continued ("Gelecek haftayı ekle"): it is not a
    /// continuation of started work, so a later plan may leave it out or plan its topic afresh (rehearsal 15.11.2026).
    /// </param>
    internal static StudyPlanTask Copy(StudyPlanRevision plan, Placement placement, bool carry = true)
    {
        var prior=placement.Source;
        var copy=StudyPlanTask.Create(plan,placement.Date,prior.Title,placement.Minutes,prior.TopicId,prior.IsPinned,prior.SourceName,prior.RemainingMinutes.HasValue?prior.RemainingQuestions:prior.PlannedQuestions);
        if(carry)copy.CarryFrom(prior);
        if(prior.PlanKind is {} kind)copy.SetPlanDecision(kind,prior.PlanReason);
        if(placement.Start is {} at)
        {
            if(prior.WeeklyLessonId is {} lesson)copy.SetWeeklyPlacement(lesson,prior.LessonNameSnapshot!,at);
            else copy.Schedule(placement.Date,at);
        }
        return copy;
    }
    internal static StudyPlanTaskOutcome CopyLink(StudyPlanTask copy, StudyPlanTaskOutcome old)
    {
        var link=StudyPlanTaskOutcome.Create(copy.Id,old.OutcomeId,old.EstimateId,old.AllocatedMinutes,old.Origin);
        if(old.CodeSnapshot is not null&&old.NameSnapshot is not null)link.CaptureText(old.CodeSnapshot,old.NameSnapshot);
        return link;
    }
    internal static async Task<WeeklyDecision[]> RebindAsync(CoachingDbContext db, StudyPlanRevision plan, Guid actor, CancellationToken ct)
    {
        var tasks=await db.StudyPlanTasks.Where(x=>x.StudentId==plan.StudentId&&x.RevisionId==plan.Id).ToArrayAsync(ct);
        var copies=tasks.Where(x=>x.PredecessorTaskId.HasValue).ToArray();
        var sourceIds=copies.Select(x=>x.PredecessorTaskId!.Value).ToArray();
        var sources=await db.StudyPlanTasks.AsNoTracking().Where(x=>x.StudentId==plan.StudentId&&Enumerable.Contains(sourceIds,x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        foreach(var copy in copies.Where(_=>!plan.WasPublished))
            if(!sources.TryGetValue(copy.PredecessorTaskId!.Value,out var source)||source.Version!=copy.PredecessorVersion)throw Conflict("Devredilen çalışma veya kalan kapsam değişti. Planı güncel verilerle yeniden hazırlayın.");
        foreach(var required in await SourcesAsync(db,plan.StudentId,ct))
            if(!tasks.Any(x=>(x.OriginalTaskId ?? x.Id)==(required.OriginalTaskId ?? required.Id)))throw Conflict("Başlanmış veya sabit çalışmanın devamı yeni planda eksik. Güncel önizlemeden planı yeniden oluşturun.");
        var decisions=await db.Set<WeeklyDecision>().Where(x=>x.StudentId==plan.StudentId&&x.StudyTaskId!=null&&(x.Status=="Planned"||x.Status=="InProgress")).ToArrayAsync(ct);
        var ids=decisions.Select(x=>x.StudyTaskId!.Value).ToArray();
        var originals=await db.StudyPlanTasks.AsNoTracking().Where(x=>x.StudentId==plan.StudentId&&Enumerable.Contains(ids,x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        var changed=new List<WeeklyDecision>();
        foreach(var decision in decisions)
        {
            if(!originals.TryGetValue(decision.StudyTaskId!.Value,out var original))continue;
            var root=original.OriginalTaskId ?? original.Id;
            var matches=tasks.Where(x=>(x.OriginalTaskId ?? x.Id)==root).ToArray();
            if(original.IsCompleted&&matches.Length==0)continue;
            if(matches.Length==0) {
                var done=await (from task in db.StudyPlanTasks.AsNoTracking() join revision in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals revision.Id
                    where task.StudentId==plan.StudentId&&revision.StudentId==plan.StudentId&&task.IsCompleted&&(task.OriginalTaskId ?? task.Id)==root
                        &&(revision.IsActive||revision.WasPublished)
                    orderby task.CompletedAt descending,task.Id select task).FirstOrDefaultAsync(ct);
                if(done is not null){decision.ContinueStudyWork(done.Id,actor,DateTime.UtcNow);changed.Add(decision);continue;}
            }
            if(matches.Length>1)throw Conflict("Kararın devam çalışması belirsiz. Planı kontrol edin.");
            if(matches.Length==1 && matches[0].Id!=decision.StudyTaskId) {decision.ContinueStudyWork(matches[0].Id,actor,DateTime.UtcNow);changed.Add(decision);}
            else if(matches.Length==0 && !original.IsCompleted)throw Conflict("Açık karara bağlı çalışma yeni planda bulunmuyor. Devam çalışmasını yeni plana ekleyin.");
        }
        return changed.ToArray();
    }
}
