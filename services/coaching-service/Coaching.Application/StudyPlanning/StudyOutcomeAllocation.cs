using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record StudyOutcomeSelection(Guid OutcomeId, Guid EstimateId, int? RequiredMinutes = null);
public sealed record ResolvedStudyOutcome(Guid TopicId, Guid OutcomeId, Guid EstimateId, int Minutes, string Origin,
    string? Code = null, string? Name = null);

public static class StudyOutcomeAllocation
{
    public static IReadOnlyList<ResolvedStudyOutcome> Deduplicate(IEnumerable<ResolvedStudyOutcome> values)
    {
        var result = new List<ResolvedStudyOutcome>();
        foreach (var group in values.GroupBy(x => x.OutcomeId))
        {
            var first = group.First();
            if (group.Any(x => x.EstimateId != first.EstimateId || x.Minutes != first.Minutes || x.Origin != first.Origin))
                throw new ArgumentException("Aynı kazanım farklı sürelerle seçilemez.");
            result.Add(first);
        }
        return result;
    }

    public static IReadOnlyList<StudyPlanTaskOutcome> Allocate(IReadOnlyList<StudyPlanTask> tasks, IReadOnlyList<ResolvedStudyOutcome> outcomes)
    {
        var result = new List<StudyPlanTaskOutcome>();
        foreach (var group in outcomes.GroupBy(x => x.TopicId))
        {
            var remaining = group.Select(x => (Value: x, Minutes: x.Minutes)).ToArray(); var index = 0;
            foreach (var task in tasks.Where(x => x.TopicId == group.Key))
            {
                var capacity = task.PlannedMinutes;
                while (capacity > 0 && index < remaining.Length)
                {
                    var (value, minutes) = remaining[index]; var assigned = Math.Min(capacity, minutes);
                    var link = StudyPlanTaskOutcome.Create(task.Id, value.OutcomeId, value.EstimateId, assigned, value.Origin);
                    if (value.Code is not null && value.Name is not null) link.CaptureText(value.Code, value.Name);
                    result.Add(link);
                    capacity -= assigned; remaining[index].Minutes -= assigned;
                    if (remaining[index].Minutes == 0) index++;
                }
            }
        }
        return result;
    }
}
