using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

/// <summary>
/// Roadmap O7: the rule-based plan engine (version 2). It reshapes the curriculum work before the weekly scheduler
/// places it: setup declarations, lesson difficulty and priority, exam errors and, when time is short, exam yield.
/// No AI is involved; every task keeps a short reason the student can read.
/// </summary>
public static class WeeklyPlanEngine
{
    public const int CurrentVersion = 2;
    public const int SpacedMinutes = 15;
    public static readonly IReadOnlyList<int> SpacedIntervalDays = [1, 7, 30];
    public static WeeklyPlanRules Rules { get; } = new(SpacedIntervalDays, SpacedMinutes);

    public sealed record LessonPreference(int Priority, int? Difficulty);

    /// <summary>A self-reported difficulty of 1 (easy) shortens learning time, 5 (hard) stretches it.</summary>
    public static decimal DifficultyFactor(int? difficulty) => difficulty switch
    {
        1 => .85m, 2 => .92m, 4 => 1.15m, 5 => 1.3m, _ => 1m
    };

    /// <summary>One question per 1.5 practice minutes; reviews are mostly practice.</summary>
    public static int? Questions(string kind, int minutes, double practiceShare)
    {
        var share = kind switch { "Learn" => practiceShare, "Review" => Math.Max(practiceShare, .5), _ => .7 };
        var questions = (int)Math.Round(minutes * Math.Clamp(share, 0, 1) / 1.5, MidpointRounding.AwayFromZero);
        return questions > 0 ? questions : null;
    }

    public static IReadOnlyList<WeeklyLessonWork> Shape(
        IReadOnlyList<WeeklyLessonWork> lessons,
        IReadOnlyDictionary<Guid, DeclaredTopicStatus> topicStates,
        IReadOnlyDictionary<Guid, LessonPreference> preferences,
        IReadOnlySet<Guid> examErrorTopics)
    {
        return lessons.Select(lesson =>
        {
            var preference = preferences.GetValueOrDefault(lesson.LessonId);
            var factor = DifficultyFactor(preference?.Difficulty);
            var outcomes = lesson.Outcomes.Select(outcome =>
            {
                var reasons = new List<string>();
                if (examErrorTopics.Contains(outcome.TopicId)) reasons.Add("Son denemelerde bu konuda hata oranın yüksek; öne alındı");
                var status = topicStates.TryGetValue(outcome.TopicId, out var declared) ? declared : DeclaredTopicStatus.NotStudied;
                string kind; int minutes;
                switch (status)
                {
                    case DeclaredTopicStatus.Studied:
                        kind = "Refresh"; minutes = Math.Max(10, (int)Math.Ceiling(outcome.Minutes * .25m));
                        reasons.Add("Çalıştım dediğin konu: kısa bir tekrar olarak planda kaldı");
                        break;
                    case DeclaredTopicStatus.NeedsReview:
                        kind = "Review"; minutes = (int)Math.Ceiling(outcome.Minutes * .6m * factor);
                        reasons.Add("Tekrar gerek dediğin konu: kısaltılmış tekrar");
                        break;
                    default:
                        kind = "Learn"; minutes = (int)Math.Ceiling(outcome.Minutes * factor);
                        break;
                }
                if (kind != "Refresh" && factor > 1) reasons.Add("Bu dersi zor bulduğun için süre artırıldı");
                if (kind != "Refresh" && factor < 1) reasons.Add("Bu dersi kolay bulduğun için süre kısaltıldı");
                if (reasons.Count == 0) reasons.Add("Müfredat sırasındaki yeni konu");
                return outcome with { Kind = kind, Minutes = Math.Clamp(minutes, 1, 1440), Reason = string.Join(" · ", reasons) };
            }).ToArray();
            return lesson with
            {
                Priority = preference?.Priority ?? 3,
                Outcomes = OrderTopics(outcomes, topic => examErrorTopics.Contains(topic) ? 0 : 1)
            };
        }).ToArray();
    }

    /// <summary>For lessons that do not fit before the end date, exam-error and high-yield topics go first.</summary>
    public static IReadOnlyList<WeeklyLessonWork> PrioritizeByYield(
        IReadOnlyList<WeeklyLessonWork> lessons,
        IReadOnlyCollection<Guid> shortLessons,
        IReadOnlyDictionary<Guid, int> yieldWeights,
        IReadOnlySet<Guid> examErrorTopics)
    {
        int Weight(Guid topic) => yieldWeights.TryGetValue(topic, out var weight) ? weight : 3;
        return lessons.Select(lesson =>
        {
            if (!shortLessons.Contains(lesson.LessonId)) return lesson;
            var outcomes = lesson.Outcomes.Select(outcome => Weight(outcome.TopicId) >= 4
                ? outcome with { Reason = Join("Sınavda sık çıkan konu; süre yetmediği için öne alındı", outcome.Reason) }
                : outcome).ToArray();
            return lesson with { Outcomes = OrderTopics(outcomes, topic => (examErrorTopics.Contains(topic) ? 0 : 10) + (5 - Weight(topic))) };
        }).ToArray();
    }

    private static string Join(string first, string? rest) =>
        string.IsNullOrWhiteSpace(rest) || rest == "Müfredat sırasındaki yeni konu" ? first : first + " · " + rest;

    /// <summary>Stable topic ordering that keeps the outcomes of a topic together and in their curriculum order.</summary>
    private static WeeklyOutcomeWork[] OrderTopics(IReadOnlyList<WeeklyOutcomeWork> outcomes, Func<Guid, int> key)
    {
        var topics = outcomes.Select(x => x.TopicId).Distinct().Select((topic, index) => (topic, index))
            .OrderBy(x => key(x.topic)).ThenBy(x => x.index).Select(x => x.topic).ToArray();
        var byTopic = outcomes.ToLookup(x => x.TopicId);
        return topics.SelectMany(topic => byTopic[topic]).ToArray();
    }
}
