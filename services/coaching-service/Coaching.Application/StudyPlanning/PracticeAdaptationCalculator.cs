using System.Text.Json;
using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record PracticeTopicAdvice(string SectionCode, Guid TopicId, int Appearances, int Correct, int Wrong, int Empty,
    string? Reason, int SuggestedMinutes);
public sealed record PracticeAdvice(string ExamCode, string Source, IReadOnlyList<Guid> ExamIds, IReadOnlyList<PracticeTopicAdvice> Topics);

public static class PracticeAdaptationCalculator
{
    public static string Identity(PracticeEvidence evidence) => evidence.Analysis?.TestIdentity is { Length: > 0 } key
        ? evidence.Input.ExamCode + ":key:" + key.Trim().ToUpperInvariant()
        : evidence.Input.ExamCode + ":unlabelled:" + evidence.ExamDate.Date.ToString("yyyy-MM-dd") + ":"
            + JsonSerializer.Serialize(evidence.Input.Sections.OrderBy(x => x.SectionCode));

    public static IReadOnlyList<PracticeAdvice> Calculate(IEnumerable<PracticeEvidence> evidence)
    {
        // Prefer the teacher record for duplicate tests. Unlabelled identical same-day answers
        // are conservatively counted once, rather than manufacturing independent evidence.
        var distinct = evidence.GroupBy(Identity).Select(g => g.OrderByDescending(x => x.Source == "TeacherRecorded")
            .ThenByDescending(x => x.RecordedAt).ThenBy(x => x.ExamId).First());
        return distinct.GroupBy(x => new { x.Input.ExamCode, x.Source }).Select(group => {
            var last = group.OrderByDescending(x => x.ExamDate).ThenByDescending(x => x.RecordedAt).ThenBy(x => x.ExamId).Take(3).ToArray();
            var advice = new List<PracticeTopicAdvice>();
            if (last.Length == 3)
            {
                foreach (var topic in last.SelectMany(x => x.Analysis?.Topics ?? []).GroupBy(x => new { x.SectionCode, x.TopicId }))
                {
                    var recent = last[0].Analysis?.Topics.SingleOrDefault(x => x.SectionCode == topic.Key.SectionCode && x.TopicId == topic.Key.TopicId);
                    var count = topic.Sum(x => x.Correct + x.Wrong + x.Empty);
                    // Product heuristics, not a mastery diagnosis. At least two independent
                    // appearances and five questions are required; recovered topics are omitted.
                    if (topic.Count() < 2 || count < 5 || recent is null
                        || (recent.Wrong + recent.Empty) / (decimal)(recent.Correct + recent.Wrong + recent.Empty) < .4m
                        || topic.Sum(x => x.Wrong + x.Empty) / (decimal)count < .4m) continue;
                    var reason = topic.Where(x => x.Reason != null).GroupBy(x => x.Reason)
                        .OrderByDescending(x => x.Count()).ThenBy(x => x.Key).FirstOrDefault()?.Key;
                    var minutes = reason == "Knowledge" && topic.Sum(x => x.Wrong + x.Empty) / (decimal)count >= .5m ? 60 : 30;
                    advice.Add(new(topic.Key.SectionCode, topic.Key.TopicId, topic.Count(), topic.Sum(x => x.Correct),
                        topic.Sum(x => x.Wrong), topic.Sum(x => x.Empty), reason, minutes));
                }
            }
            return new PracticeAdvice(group.Key.ExamCode, group.Key.Source, last.Select(x => x.ExamId).ToArray(),
                advice.OrderByDescending(x => x.Wrong + x.Empty).ThenBy(x => x.TopicId).ToArray());
        }).OrderBy(x => x.ExamCode).ThenBy(x => x.Source).ToArray();
    }
}
