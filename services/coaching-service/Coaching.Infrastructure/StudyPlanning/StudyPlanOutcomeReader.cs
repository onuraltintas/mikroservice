using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

internal static class StudyPlanOutcomeReader
{
    internal static async Task<ILookup<Guid, StudyTaskOutcomeView>> ReadAsync(CoachingDbContext db, Guid[] taskIds, CancellationToken ct)
    {
        var rows = await (from link in db.Set<StudyPlanTaskOutcome>().AsNoTracking()
            join outcome in db.Set<StudyCatalogOutcome>() on link.OutcomeId equals outcome.Id
            join estimate in db.Set<OutcomeDurationEstimate>() on link.EstimateId equals estimate.Id
            where Enumerable.Contains(taskIds, link.TaskId)
            orderby outcome.DisplayOrder
            select new { link.TaskId, View = new StudyTaskOutcomeView(outcome.Id, outcome.Code, outcome.Name, estimate.Id,
                estimate.Number, link.AllocatedMinutes, link.Origin) }).ToArrayAsync(ct);
        return rows.ToLookup(x => x.TaskId, x => x.View);
    }
}
