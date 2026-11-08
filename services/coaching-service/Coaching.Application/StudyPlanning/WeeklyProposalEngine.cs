using System.Globalization;
using System.Text.RegularExpressions;
using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

/// <summary>Paket H2: Fill keeps the saved weekly cards and proposes only in free time; Replace proposes the whole week.</summary>
public enum WeeklyProposalMode { Fill, Replace }
public sealed record WeeklyProposalWindow(DayOfWeek Day, int StartMinute, int EndMinute);
/// <summary>NeedMinutes: the lesson's weekly need (its curriculum work and spaced reviews spread over the weeks until the plan ends).</summary>
public sealed record WeeklyProposalLesson(Guid LessonId, string Name, string? ExamCode, int NeedMinutes, int Priority = 3, bool Hard = false);
/// <summary>SessionsBeforeBreak: 1 for middle school (a break after every session), 2 for everyone else (K1).</summary>
public sealed record WeeklyProposalInput(IReadOnlyList<WeeklyProposalWindow> Windows, IReadOnlyList<WeeklyDraftSlot> Existing,
    IReadOnlyList<WeeklyProposalLesson> Lessons, int SessionsBeforeBreak, int MaxDayMinutes, string? ExamFocus, WeeklyProposalMode Mode);
public sealed record WeeklyProposedSlot(Guid LessonId, DayOfWeek Day, int StartMinute, int DurationMinutes);
/// <summary>NeededMinutes: the unweighted weekly need rounded up to 30 minutes. NoTime: neither a saved nor a proposed card.</summary>
public sealed record WeeklyProposalLessonResult(Guid LessonId, string Name, int ExistingMinutes, int ProposedMinutes, int NeededMinutes, bool NoTime);
public sealed record WeeklyProposalResult(IReadOnlyList<WeeklyProposedSlot> Slots, IReadOnlyList<WeeklyProposalLessonResult> Lessons,
    int FreeMinutes, int NeededMinutes, int ShortageMinutes);

/// <summary>
/// Paket H2: the need-based weekly proposal. Hours go to the chosen lessons by their weekly need (weighted by priority and, for YKS,
/// the exam focus), in 60-minute sessions on the half hour with 30-minute breaks (K1), never above the daily limit (K5).
/// Pure and deterministic: no clock, no database, no random ids.
/// </summary>
public static class WeeklyProposalEngine
{
    public const int SessionMinutes = 60;
    public const int BreakMinutes = 30;
    /// <summary>The week as the student sees it: Monday first.</summary>
    public static readonly IReadOnlyList<DayOfWeek> Week =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly Regex ExamSuffix = new(@"^(.*\S)\s*\([^()]*\)\s*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static int SessionsBeforeBreak(bool middleSchool) => middleSchool ? 1 : 2;

    /// <summary>Priority 1 is the highest; 3 is the default.</summary>
    public static decimal PriorityFactor(int priority) => priority switch { 1 => 1.3m, 2 => 1.15m, 4 => .9m, 5 => .8m, _ => 1m };

    /// <summary>K6: the YKS exam the student puts first gets ×1.3, the other ×0.75; "balanced" or none changes nothing.</summary>
    public static decimal FocusFactor(string? focus, string? examCode) => (focus?.Trim().ToLowerInvariant(), examCode?.Trim().ToUpperInvariant()) switch
    {
        ("tyt", "TYT") => 1.3m, ("tyt", "AYT") => .75m, ("ayt", "AYT") => 1.3m, ("ayt", "TYT") => .75m, _ => 1m
    };

    /// <summary>"Matematik (TYT)" and "Matematik (AYT)" are one family: the name without a trailing part in parentheses, Turkish lower case.</summary>
    public static string Family(string name)
    {
        var text = (name ?? "").Trim();
        var match = ExamSuffix.Match(text);
        return (match.Success ? match.Groups[1].Value : text).Trim().ToLower(Turkish);
    }

    public static WeeklyProposalResult Propose(WeeklyProposalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Windows is null || input.Existing is null || input.Lessons is null || input.SessionsBeforeBreak < 1)
            throw new ArgumentException("Haftalık öneri bilgilerini kontrol edin.");
        var fill = input.Mode == WeeklyProposalMode.Fill;
        var existing = fill ? input.Existing.Where(x => x is not null).ToArray() : [];
        var lessons = input.Lessons.Where(x => x is not null).DistinctBy(x => x.LessonId).ToArray();
        var sessions = FreeSessions(input.Windows, existing, input.SessionsBeforeBreak, input.MaxDayMinutes);
        var plans = lessons.Select(lesson => Plan.For(lesson, existing.Where(x => x.LessonId == lesson.LessonId).Sum(x => x.DurationMinutes), input.ExamFocus)).ToArray();
        var counts = Allocate(plans, sessions.Count);
        var total = counts.Values.Sum();
        var chosen = total >= sessions.Count ? sessions : Spread(sessions, total);
        var placed = Place(chosen, sessions, plans, counts, existing);
        var results = plans.Select(plan =>
        {
            var proposed = placed.Where(x => x.LessonId == plan.Lesson.LessonId).Sum(x => x.DurationMinutes);
            return new WeeklyProposalLessonResult(plan.Lesson.LessonId, plan.Lesson.Name, plan.Existing, proposed, plan.Needed, plan.Existing + proposed == 0);
        }).ToArray();
        var shortage = results.Sum(x => Math.Max(0, x.NeededMinutes - x.ExistingMinutes - x.ProposedMinutes));
        return new(placed, results, sessions.Count * SessionMinutes, results.Sum(x => x.NeededMinutes), shortage);
    }

    /// <summary>
    /// Per lesson: Weighted = need × priority factor × exam focus. The lesson wants ceil(max(need, weighted) ÷ 60) sessions (at least one);
    /// it needs ceil(need ÷ 60) (at least one). In Fill mode its saved minutes count first.
    /// </summary>
    private sealed record Plan(WeeklyProposalLesson Lesson, int Existing, decimal Weighted, int Needed, int NeedNew, int WantNew)
    {
        public static Plan For(WeeklyProposalLesson lesson, int existing, string? focus)
        {
            var need = Math.Max(0, lesson.NeedMinutes);
            var priority = lesson.Priority is >= 1 and <= 5 ? lesson.Priority : 3;
            var weighted = need * PriorityFactor(priority) * FocusFactor(focus, lesson.ExamCode);
            var needTotal = Math.Max(1, (int)Math.Ceiling(need / (double)SessionMinutes));
            var wantTotal = Math.Max(needTotal, (int)Math.Ceiling(weighted / SessionMinutes));
            int New(int sessions) => Math.Max(0, (int)Math.Ceiling((sessions * SessionMinutes - existing) / (double)SessionMinutes));
            return new(lesson with { Priority = priority }, existing, weighted, (int)Math.Ceiling(need / 30d) * 30, New(needTotal), New(wantTotal));
        }
        /// <summary>How long the lesson would take with this many sessions: the larger, the later it finishes.</summary>
        public decimal Load(int sessions) => Weighted / (Existing / (decimal)SessionMinutes + sessions);
    }

    /// <summary>
    /// When every lesson's wanted sessions fit, each gets them. Otherwise every lesson still needing time gets one (or, with fewer sessions
    /// than such lessons, the ones with the largest weighted need do), then each remaining session goes to the lesson that would finish
    /// latest: first up to its need, then up to its wanted sessions.
    /// </summary>
    private static Dictionary<Guid, int> Allocate(IReadOnlyList<Plan> plans, int capacity)
    {
        var counts = plans.ToDictionary(x => x.Lesson.LessonId, _ => 0);
        if (plans.Sum(x => x.WantNew) <= capacity)
        {
            foreach (var plan in plans) counts[plan.Lesson.LessonId] = plan.WantNew;
            return counts;
        }
        var needing = plans.Where(x => x.NeedNew > 0).OrderByDescending(x => x.Weighted).ThenBy(x => x.Lesson.LessonId).ToArray();
        if (needing.Length >= capacity)
        {
            foreach (var plan in needing.Take(capacity)) counts[plan.Lesson.LessonId] = 1;
            return counts;
        }
        foreach (var plan in needing) counts[plan.Lesson.LessonId] = 1;
        var left = capacity - needing.Length;
        foreach (var cap in new Func<Plan, int>[] { x => x.NeedNew, x => x.WantNew })
            while (left > 0 && plans.Where(x => counts[x.Lesson.LessonId] < cap(x)).OrderByDescending(x => x.Load(counts[x.Lesson.LessonId]))
                       .ThenBy(x => x.Lesson.LessonId).FirstOrDefault() is { } latest)
            {
                counts[latest.Lesson.LessonId]++; left--;
            }
        return counts;
    }

    private static int DayIndex(DayOfWeek day) => ((int)day + 6) % 7;

    /// <summary>
    /// Every 60-minute session that fits: on :00/:30 inside the availability windows, clear of saved cards, never making a run of
    /// study (saved or proposed, back to back) longer than <paramref name="sessionsBeforeBreak"/> hours, and within the daily limit.
    /// A piece of free time shorter than an hour stays empty.
    /// </summary>
    private static List<(DayOfWeek Day, int Start)> FreeSessions(IReadOnlyList<WeeklyProposalWindow> windows, IReadOnlyList<WeeklyDraftSlot> existing,
        int sessionsBeforeBreak, int maxDayMinutes)
    {
        var result = new List<(DayOfWeek, int)>();
        var longestRun = sessionsBeforeBreak * SessionMinutes;
        foreach (var day in Week)
        {
            var busy = existing.Where(x => x.Day == day).Select(x => (From: x.StartMinute, To: x.StartMinute + x.DurationMinutes)).ToList();
            var used = busy.Sum(x => x.To - x.From);
            var spans = windows.Where(x => x is not null && x.Day == day)
                .Select(x => (From: Math.Max(0, x.StartMinute), To: Math.Min(1440, x.EndMinute))).Where(x => x.To > x.From).OrderBy(x => x.From).ToList();
            var merged = new List<(int From, int To)>();
            foreach (var span in spans)
                if (merged.Count > 0 && span.From <= merged[^1].To) merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, span.To));
                else merged.Add(span);
            foreach (var span in merged)
                for (var start = (span.From + 29) / 30 * 30; start + SessionMinutes <= span.To && used + SessionMinutes <= maxDayMinutes;)
                {
                    var end = start + SessionMinutes;
                    if (busy.Any(x => start < x.To && end > x.From) || Run(busy, start, end) > longestRun) { start += 30; continue; }
                    busy.Add((start, end)); used += SessionMinutes; result.Add((day, start)); start = end;
                }
        }
        return result;
    }

    /// <summary>The length of the back-to-back study block a new session would belong to.</summary>
    private static int Run(List<(int From, int To)> busy, int start, int end)
    {
        for (var i = busy.FindIndex(x => x.To == start); i >= 0; i = busy.FindIndex(x => x.To == start)) start = busy[i].From;
        for (var i = busy.FindIndex(x => x.From == end); i >= 0; i = busy.FindIndex(x => x.From == end)) end = busy[i].To;
        return end - start;
    }

    /// <summary>The first free session of every day, then the second of every day, and so on, until <paramref name="count"/> are chosen.</summary>
    private static List<(DayOfWeek Day, int Start)> Spread(IReadOnlyList<(DayOfWeek Day, int Start)> sessions, int count)
    {
        var byDay = Week.Select(day => sessions.Where(x => x.Day == day).OrderBy(x => x.Start).ToArray()).ToArray();
        var chosen = new List<(DayOfWeek, int)>();
        for (var round = 0; chosen.Count < count && byDay.Any(x => x.Length > round); round++)
            foreach (var day in byDay)
                if (day.Length > round && chosen.Count < count) chosen.Add(day[round]);
        return chosen;
    }

    /// <summary>
    /// Goes through the chosen sessions in day and time order. A lesson of the same name family as the card right before or after is
    /// never placed; a lesson already on that day comes only when no other lesson is left; the day's first session goes to a priority-1
    /// or hard lesson; otherwise the lesson with the most sessions left, then the lesson id. Sessions a lesson could not take are tried
    /// again in the free sessions left unchosen.
    /// </summary>
    private static List<WeeklyProposedSlot> Place(IReadOnlyList<(DayOfWeek Day, int Start)> chosen, IReadOnlyList<(DayOfWeek Day, int Start)> all,
        IReadOnlyList<Plan> plans, Dictionary<Guid, int> counts, IReadOnlyList<WeeklyDraftSlot> existing)
    {
        var left = new Dictionary<Guid, int>(counts);
        var byId = plans.ToDictionary(x => x.Lesson.LessonId);
        var families = plans.ToDictionary(x => x.Lesson.LessonId, x => Family(x.Lesson.Name));
        var existingCards = existing.Select(x => (x.LessonId, x.Day, From: x.StartMinute, To: x.StartMinute + x.DurationMinutes, Family: Family(x.LessonName))).ToArray();
        var placed = new List<WeeklyProposedSlot>();
        var used = new HashSet<(DayOfWeek, int)>();
        void Pass(IEnumerable<(DayOfWeek Day, int Start)> sessions)
        {
            foreach (var (day, start) in sessions.OrderBy(x => DayIndex(x.Day)).ThenBy(x => x.Start))
            {
                if (used.Contains((day, start)) || left.Values.All(x => x == 0)) continue;
                var end = start + SessionMinutes;
                var cards = existingCards.Select(x => (x.LessonId, x.Day, x.From, x.To, x.Family))
                    .Concat(placed.Select(x => (x.LessonId, x.Day, From: x.StartMinute, To: x.StartMinute + x.DurationMinutes, Family: families[x.LessonId])))
                    .Where(x => x.Day == day).ToArray();
                var neighbours = cards.Where(x => x.To == start || x.From == end).Select(x => x.Family).ToHashSet();
                var today = cards.Select(x => x.LessonId).ToHashSet();
                var first = !placed.Any(x => x.Day == day && x.StartMinute < start);
                var pick = left.Where(x => x.Value > 0 && !neighbours.Contains(families[x.Key])).Select(x => byId[x.Key])
                    .OrderBy(x => today.Contains(x.Lesson.LessonId) ? 1 : 0)
                    .ThenBy(x => first && (x.Lesson.Priority == 1 || x.Lesson.Hard) ? 0 : 1)
                    .ThenByDescending(x => left[x.Lesson.LessonId]).ThenBy(x => x.Lesson.LessonId).FirstOrDefault();
                if (pick is null) continue;
                placed.Add(new(pick.Lesson.LessonId, day, start, SessionMinutes)); used.Add((day, start)); left[pick.Lesson.LessonId]--;
            }
        }
        Pass(chosen);
        if (left.Values.Any(x => x > 0)) Pass(all);
        return placed.OrderBy(x => DayIndex(x.Day)).ThenBy(x => x.StartMinute).ToList();
    }
}
