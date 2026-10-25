using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace Coaching.Infrastructure.StudyPlanning;

internal static class CoachingCompletedOutcomeCredits
{
    internal sealed record Evidence(StudyPlanTask Task,StudyPlanTaskOutcome Link,bool IsTopicShare);
    internal static async Task<Evidence[]> ReadAsync(CoachingDbContext db,Guid student,Guid[] outcomes,CancellationToken ct)
    {
        var rows=await (from task in db.StudyPlanTasks.AsNoTracking()
            join plan in db.StudyPlanRevisions.AsNoTracking() on task.RevisionId equals plan.Id
            join link in db.Set<StudyPlanTaskOutcome>().AsNoTracking() on task.Id equals link.TaskId
            where task.StudentId==student&&plan.StudentId==student&&task.IsCompleted&&(plan.IsActive||plan.WasPublished)
                && Enumerable.Contains(outcomes,link.OutcomeId)
            orderby task.CompletedAt,task.CompletionSourceTaskId!=null,task.Id,link.OutcomeId select new {Task=task,Link=link}).ToArrayAsync(ct);
        var versions=rows.Select(x=>x.Link.EstimateId).Distinct().ToArray();
        var scoped=(await db.Set<StudyCatalogTopicOutcomeShare>().AsNoTracking().Where(x=>Enumerable.Contains(versions,x.EstimateId))
            .Select(x=>x.EstimateId).Distinct().ToArrayAsync(ct)).ToHashSet();
        return rows.DistinctBy(x=>(x.Task.OriginalTaskId??x.Task.Id,x.Link.OutcomeId))
            .Select(x=>new Evidence(x.Task,x.Link,scoped.Contains(x.Link.EstimateId))).ToArray();
    }

    internal static string Fingerprint(IEnumerable<Evidence> credits)=>Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        credits.OrderBy(x=>x.Task.Id).ThenBy(x=>x.Link.OutcomeId).Select(x=>new{x.Task.Id,x.Task.Version,x.Task.OriginalTaskId,x.Task.TopicId,x.Link.OutcomeId,x.Link.EstimateId,x.Link.AllocatedMinutes,x.IsTopicShare}))));

    // A completed allocation is curriculum coverage, regardless of its actual effort or estimate version.
    // Global allocations are consumed once across selected topics; topic shares stay on their own topic.
    internal static ResolvedStudyOutcome[] Subtract(IReadOnlyList<ResolvedStudyOutcome> selected,IEnumerable<Evidence> evidence)
    {
        var remaining=selected.ToArray();
        foreach(var credit in evidence)
        {
            var left=credit.Link.AllocatedMinutes;
            foreach(var index in Enumerable.Range(0,remaining.Length).Where(i=>remaining[i].OutcomeId==credit.Link.OutcomeId
                &&(!credit.IsTopicShare||remaining[i].TopicId==credit.Task.TopicId)).OrderByDescending(i=>remaining[i].TopicId==credit.Task.TopicId))
            {
                var minutes=Math.Min(left,remaining[index].Minutes);
                remaining[index]=remaining[index] with {Minutes=remaining[index].Minutes-minutes};left-=minutes;
            }
        }
        return remaining.Where(x=>x.Minutes>0).ToArray();
    }
}
