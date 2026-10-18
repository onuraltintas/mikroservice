using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public static class OutcomeScopeBudget
{
    public static IReadOnlyList<StudyCatalogTopicOutcomeShare> Rebase(OutcomeDurationEstimate estimate,
        IReadOnlyList<StudyCatalogTopicOutcomeShare> previous)
    {
        if (previous.Count == 0 || estimate.TotalMinutes < previous.Count)
            throw new ArgumentException("Her konu kapsamına en az bir dakika ayırın.");
        var ordered = previous.OrderBy(x => x.TopicId).ToArray();
        var learning = Split(estimate.LearningMinutes, ordered.Select(x => x.LearningMinutes).ToArray());
        var practice = Split(estimate.PracticeMinutes, ordered.Select(x => x.PracticeMinutes).ToArray());
        var check = Split(estimate.CheckMinutes, ordered.Select(x => x.CheckMinutes).ToArray());
        var components = new[] { learning, practice, check };
        for (var i = 0; i < ordered.Length; i++)
        {
            if (components.Sum(x => x[i]) != 0) continue;
            var donor = Enumerable.Range(0, ordered.Length).Where(j => components.Sum(x => x[j]) > 1)
                .OrderByDescending(j => components.Sum(x => x[j])).First();
            var component = components.OrderByDescending(x => x[donor]).First();
            component[donor]--; component[i]++;
        }
        return ordered.Select((x, i) => StudyCatalogTopicOutcomeShare.Create(x.TopicId, estimate,
            learning[i], practice[i], check[i], x.ScopeDescription)).ToArray();
    }

    private static int[] Split(int total, int[] weights)
    {
        var sum = weights.Sum();
        if (sum == 0) { weights = weights.Select(_ => 1).ToArray(); sum = weights.Length; }
        var result = weights.Select(x => (int)((long)total * x / sum)).ToArray();
        var order = Enumerable.Range(0, weights.Length).OrderByDescending(i => (long)total * weights[i] % sum).ThenBy(i => i).ToArray();
        var remaining = total - result.Sum();
        for (var i = 0; i < remaining; i++) result[order[i]]++;
        return result;
    }

    public static void Validate(OutcomeDurationEstimate estimate, IReadOnlyList<StudyCatalogTopicOutcomeShare> shares,
        IReadOnlyCollection<Guid> approvedTopics)
    {
        if (shares.Count == 0 || shares.Select(x => x.TopicId).Distinct().Count() != shares.Count
            || !shares.Select(x => x.TopicId).ToHashSet().SetEquals(approvedTopics)
            || shares.Any(x => x.EstimateId != estimate.Id || x.OutcomeId != estimate.OutcomeId)
            || shares.Sum(x => (long)x.LearningMinutes) != estimate.LearningMinutes
            || shares.Sum(x => (long)x.PracticeMinutes) != estimate.PracticeMinutes
            || shares.Sum(x => (long)x.CheckMinutes) != estimate.CheckMinutes)
            throw new ArgumentException("Konu payları onaylı kapsamın tamamını ve kazanımın her süre bileşenini tam karşılamalıdır.");
    }
}
