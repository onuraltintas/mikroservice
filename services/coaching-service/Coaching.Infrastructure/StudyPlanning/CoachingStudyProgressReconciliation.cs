using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

// Reconcile state and declared scope, never effort. Every task keeps its own work-day ledger.
internal static class CoachingStudyProgressReconciliation
{
    internal static bool RequiresExplicitScope(StudyPlanTask task)
        => task.ActualMinutes.HasValue || task.RemainingMinutes.HasValue || task.RealizationHistory.Any(x=>x.Action=="RemainingScope");

    internal static async Task RestoreAsync(CoachingDbContext db,StudyPlanRevision target,Guid actor,CancellationToken ct)
    {
        if(!target.WasPublished)return; // Unpublished archives still use the draft source/version guards.
        var tasks=await db.StudyPlanTasks.Where(x=>x.RevisionId==target.Id&&x.StudentId==target.StudentId).ToArrayAsync(ct);
        var roots=tasks.Select(x=>x.OriginalTaskId??x.Id).ToArray();
        if(roots.Distinct().Count()!=roots.Length)throw CoachingStudyContinuation.Conflict("Geri açılacak planda aynı çalışmanın birden fazla devamı var.");
        var history=await (from task in db.StudyPlanTasks join plan in db.StudyPlanRevisions on task.RevisionId equals plan.Id
            where task.StudentId==target.StudentId&&plan.StudentId==target.StudentId&&(plan.WasPublished||plan.IsActive)
                && Enumerable.Contains(roots,task.OriginalTaskId??task.Id)
            select new {Task=task,Plan=plan}).ToArrayAsync(ct);
        foreach(var task in tasks)
        {
            if(task.IsCompleted&&task.CompletionSourceTaskId is null)continue;
            var chain=history.Where(x=>(x.Task.OriginalTaskId??x.Task.Id)==(task.OriginalTaskId??task.Id)).ToArray();
            var active=chain.Where(x=>x.Plan.IsActive).ToArray();
            if(active.Length>1)throw CoachingStudyContinuation.Conflict("Aynı çalışmanın birden fazla aktif devamı var.");
            var proof=chain.Where(x=>x.Task.IsCompleted&&x.Task.CompletionSourceTaskId is null)
                .OrderByDescending(x=>x.Task.CompletedAt).ThenBy(x=>x.Task.Id).FirstOrDefault();
            var current=proof??active.SingleOrDefault()??chain.OrderByDescending(x=>x.Task.CreatedAt).ThenBy(x=>x.Task.Id).FirstOrDefault();
            if(current is null||current.Task.Id==task.Id)continue;
            if(!current.Task.IsCompleted)CoachingStudyContinuation.Minutes(current.Task);
            task.ReconcileProgress(current.Task,actor,"Eski plan yeniden etkinleştirilirken güncel çalışma ilerlemesi korundu.");
        }
    }

    internal static async Task<bool> CorrectAsync(CoachingDbContext db,StudyPlanTask source,bool scopeChanged,
        StudyContinuationScopeInput? scope,Guid actor,string reason,CancellationToken ct)
    {
        var root=source.OriginalTaskId??source.Id;
        var chain=await (from task in db.StudyPlanTasks join plan in db.StudyPlanRevisions on task.RevisionId equals plan.Id
            where task.StudentId==source.StudentId&&plan.StudentId==source.StudentId&&(plan.WasPublished||plan.IsActive)
                && (task.OriginalTaskId??task.Id)==root select new {Task=task,Plan=plan}).ToArrayAsync(ct);
        var activeRows=chain.Where(x=>x.Plan.IsActive).ToArray();
        if(activeRows.Length>1)throw CoachingStudyContinuation.Conflict("Aynı çalışmanın birden fazla aktif devamı var.");
        var active=activeRows.SingleOrDefault();
        if(scope is not null && (active is null||active.Task.Id==source.Id||active.Plan.Id!=scope.RevisionId||active.Plan.Version!=scope.ExpectedVersion))
            throw CoachingStudyContinuation.Conflict("Aktif devam planı değişti. Geçmiş planı yeniden yükleyip güncel kalan kapsamı kontrol edin.");
        var proof=chain.Where(x=>x.Task.IsCompleted&&x.Task.CompletionSourceTaskId is null)
            .OrderByDescending(x=>x.Task.CompletedAt).ThenBy(x=>x.Task.Id).FirstOrDefault();
        var changedPlans=new HashSet<StudyPlanRevision>();
        if(active is not null && (active.Task.Id!=source.Id||proof is not null&&proof.Task.Id!=source.Id))
        {
            var before=active.Task.RealizationHistoryJson;
            if(proof is not null)
            {
                if(scope is not null)throw CoachingStudyContinuation.Conflict("Çalışma zincirinde tamamlanmış çalışma var; önce tamamlanma kaydını kontrol edin.");
                if(active.Task.CompletionSourceTaskId.HasValue||!active.Task.IsCompleted)
                    active.Task.ReconcileProgress(proof.Task,actor,$"Geçmiş gerçekleşme düzeltmesiyle uzlaştırıldı: {reason}");
            }
            else if(scopeChanged||scope is not null||active.Task.CompletionSourceTaskId.HasValue)
            {
                if(source.IsCompleted)throw CoachingStudyContinuation.Conflict("Tamamlanmanın kaynak kaydı bulunamadı; çalışma zincirini kontrol edin.");
                if(scope is null&&RequiresExplicitScope(active.Task))
                    throw CoachingStudyContinuation.Conflict("Aktif devamda ek çalışma veya kapsam beyanı var. Düzeltmeyle birlikte aktif devamın güncel kalan süresini belirtin.");
                if(scope is null)CoachingStudyContinuation.Minutes(source);
                active.Task.ReconcileProgress(source,actor,$"Geçmiş gerçekleşme düzeltmesiyle uzlaştırıldı: {reason}",scope?.RemainingMinutes,scope?.RemainingQuestions);
            }
            if(before!=active.Task.RealizationHistoryJson)changedPlans.Add(active.Plan);
        }
        // Previously inherited completions must not remain as independent curriculum credit
        // after their actual completion evidence is corrected or withdrawn.
        foreach(var row in chain.Where(x=>!x.Plan.IsActive&&x.Task.Id!=source.Id&&x.Task.CompletionSourceTaskId.HasValue))
        {
            var before=row.Task.RealizationHistoryJson;
            if(proof is not null)row.Task.ReconcileProgress(proof.Task,actor,$"Tamamlanma kaynağı düzeltmeyle uzlaştırıldı: {reason}");
            else
            {
                var pending=row.Task.RealizationHistory.LastOrDefault(x=>x.Action=="ContinuationReconciliation"&&x.Completed&&!x.PreviouslyCompleted);
                row.Task.ReconcileProgress(source,actor,$"Devralınan tamamlanma geri alındı: {reason}",pending?.PreviousRemainingMinutes??row.Task.PlannedMinutes,pending?.PreviousRemainingQuestions??row.Task.PlannedQuestions);
            }
            if(before!=row.Task.RealizationHistoryJson)changedPlans.Add(row.Plan);
        }
        foreach(var plan in changedPlans)plan.RecordRealizationChange();
        return changedPlans.Count>0;
    }
}
