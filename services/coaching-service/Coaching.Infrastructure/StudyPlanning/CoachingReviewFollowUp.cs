using System.Security.Cryptography;
using System.Text.Json;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;
public static class CoachingReviewFollowUp
{
    public sealed record Pending(Guid Id,Guid StudentId,DateOnly WeekStart,string Reason);
    // Compare the outcome itself, never observation time, overdue flags, permissions or unrelated row versions.
    internal static string Fingerprint(WeeklyResultSnapshot value)
    {
        object? Work(DecisionLinkedWork? x)=>x is null?null:new { x.Id,x.Title,x.Status,x.IsDone,x.PlanId,x.Date,x.StartMinute,x.Minutes,x.ActualMinutes,x.ActualQuestions,x.Score,x.CompletedAt,x.Feedback,x.SubmittedAt,x.ResultRevision };
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            Schema=1,value.Summary,
            Decisions=value.Decisions.OrderBy(x=>x.DecisionId).Select(x=>new {x.DecisionId,x.SourceWeekStart,x.Title,x.Status,x.DueOn,x.CompletedAt,Study=Work(x.Work?.Study),Assignment=Work(x.Work?.Assignment),x.LinkedStudyRootId}),
            Studies=value.Studies.OrderBy(x=>x.TaskId).Select(x=>new {x.TaskId,x.RootTaskId,x.PlanId,x.PlannedDate,x.Title,x.PlannedMinutes,x.ActualMinutes,x.ActualQuestions,x.IsCompleted,x.CompletedAt,x.ResultRevision,
                Days=x.WorkDays?.OrderBy(x=>x.WorkedOn)})
        })));
    }
    internal static long StudyRevision(StudyPlanTask task,DateOnly? from=null,DateOnly? to=null)
        =>task.RealizationHistory.LongCount(x=>((x.PreviousMinutes??0)!=x.Minutes || x.PreviousQuestions!=x.Questions || x.PreviouslyCompleted!=x.Completed || x.ReassignFromDate.HasValue)
            && (!from.HasValue || (x.WorkedOn??task.WorkFromDate??task.PlannedDate)>=from && (x.WorkedOn??task.WorkFromDate??task.PlannedDate)<=to
                || x.ReassignFromDate>=from&&x.ReassignFromDate<=to || x.PreviouslyCompleted!=x.Completed));
    internal static string? Reason(WeeklyReflection reflection,WeeklyReflectionReview? latest,string? fingerprint)
        =>Reason(reflection.Version,latest?.ReflectionVersion,latest?.EvidenceFingerprint,fingerprint);
    private static string? Reason(int version,int? reviewedVersion,string? evidence,string? fingerprint)
        => reviewedVersion is null?"ReflectionSubmitted":reviewedVersion!=version?"ReflectionUpdated":
            evidence is null?"LegacyEvidenceMissing":evidence!=fingerprint?"ResultsChanged":null;
    private sealed record ReflectionKey(Guid Id,Guid StudentId,DateOnly WeekStart,int Version);
    private sealed record ReviewEvidence(Guid ReflectionId,int ReflectionVersion,string? EvidenceFingerprint);

    // Keyset batches include only queue metadata, not the student's text or saved evidence JSON.
    // A reviewed row is skipped only after comparing its live results with the last immutable review.
    public static async IAsyncEnumerable<Pending> ScanPendingAsync(CoachingDbContext db,Guid[] students,Guid teacher,DateOnly through,
        TimeProvider clock,[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct=default)
    {
        if(students.Length==0)yield break;
        ReflectionKey? cursor=null;
        while(true)
        {
            var query=db.Set<WeeklyReflection>().AsNoTracking().Where(x=>Enumerable.Contains(students,x.StudentId)&&x.WeekStart.AddDays(6)<=through);
            if(cursor is not null) {
                var week=cursor.WeekStart;var id=cursor.Id;
                query=query.Where(x=>x.WeekStart>week||x.WeekStart==week&&x.Id.CompareTo(id)>0);
            }
            var reflections=await query.OrderBy(x=>x.WeekStart).ThenBy(x=>x.Id).Take(256)
                .Select(x=>new ReflectionKey(x.Id,x.StudentId,x.WeekStart,x.Version)).ToArrayAsync(ct);
            if(reflections.Length==0)yield break;
            var ids=reflections.Select(x=>x.Id).ToArray();
            var reviews=await db.Set<WeeklyReflectionReview>().AsNoTracking().Where(x=>x.TeacherId==teacher&&Enumerable.Contains(ids,x.ReflectionId))
                .GroupBy(x=>x.ReflectionId).Select(g=>g.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id)
                    .Select(x=>new ReviewEvidence(x.ReflectionId,x.ReflectionVersion,x.EvidenceFingerprint)).First())
                .ToDictionaryAsync(x=>x.ReflectionId,ct);
            var current=new Dictionary<(Guid Student,DateOnly Week),string>();
            var comparable=reflections.Where(x=>reviews.TryGetValue(x.Id,out var latest)&&latest.ReflectionVersion==x.Version&&latest.EvidenceFingerprint is not null);
            foreach(var group in comparable.GroupBy(x=>x.StudentId))
                foreach(var batch in group.Chunk(26))
                    await ReadFingerprintsAsync(db,clock,group.Key,batch.Select(x=>x.WeekStart).ToArray(),current,ct);
            foreach(var reflection in reflections) {
                ct.ThrowIfCancellationRequested();
                reviews.TryGetValue(reflection.Id,out var latest);current.TryGetValue((reflection.StudentId,reflection.WeekStart),out var fingerprint);
                if(Reason(reflection.Version,latest?.ReflectionVersion,latest?.EvidenceFingerprint,fingerprint) is {} reason)
                    yield return new(reflection.Id,reflection.StudentId,reflection.WeekStart,reason);
            }
            cursor=reflections[^1];
        }
    }
    private static async Task ReadFingerprintsAsync(CoachingDbContext db,TimeProvider clock,Guid student,DateOnly[] weeks,
        Dictionary<(Guid Student,DateOnly Week),string> current,CancellationToken ct)
    {
        try {
            var snapshots=await CoachingWeeklyResultReader.ReadManyAsync(db,clock,student,weeks,ct);
            foreach(var snapshot in snapshots)current[(student,snapshot.Key)]=Fingerprint(snapshot.Value);
        }
        // A wide historical batch can exceed the report's range limit although every week is valid.
        // Split the range instead of making historical volume prevent access to the queue.
        catch(ArgumentException ex) when(weeks.Length>1 && ex.Message is ("Çalışma raporu sınırı aşıldı. Daha kısa bir dönem seçin." or "Karar sonuçları inceleme sınırını aşıyor.")) {
            var half=weeks.Length/2;
            await ReadFingerprintsAsync(db,clock,student,weeks[..half],current,ct);
            await ReadFingerprintsAsync(db,clock,student,weeks[half..],current,ct);
        }
    }
    // Compatibility for callers needing the full result; tracking consumes the stream directly.
    public static async Task<Pending[]> PendingAsync(CoachingDbContext db,Guid[] students,Guid teacher,DateOnly through,TimeProvider clock,CancellationToken ct)
    {
        var pending=new List<Pending>();
        await foreach(var item in ScanPendingAsync(db,students,teacher,through,clock,ct))pending.Add(item);
        return pending.ToArray();
    }
}
