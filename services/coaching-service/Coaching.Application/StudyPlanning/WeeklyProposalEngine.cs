using System.Globalization;
using System.Text.RegularExpressions;
using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

/// <summary>Paket H2: Fill keeps the saved weekly cards and proposes only in free time; Replace proposes the whole week.</summary>
public enum WeeklyProposalMode { Fill, Replace }
public sealed record WeeklyProposalWindow(DayOfWeek Day, int StartMinute, int EndMinute);
/// <summary>NeedMinutes: the lesson's weekly need (its curriculum work and spaced reviews spread over the weeks until the plan ends).</summary>
public sealed record WeeklyProposalLesson(Guid LessonId, string Name, string? ExamCode, int NeedMinutes, int Priority = 3, bool Hard = false);
/// <summary>
/// SessionsBeforeBreak: 1 for middle school (a break after every session), 2 for everyone else (K1). DayOccurrences: how many times each
/// weekday comes before the plan ends; a weekday that does not come gets no session (null: every weekday comes).
/// DayBudgets (Paket H4a): the day budgets of a routine profile that still matches the free hours; a day then holds at most
/// min(budget, MaxDayMinutes), and a day without a budget gets no session (null: only MaxDayMinutes).
/// </summary>
public sealed record WeeklyProposalInput(IReadOnlyList<WeeklyProposalWindow> Windows, IReadOnlyList<WeeklyDraftSlot> Existing,
    IReadOnlyList<WeeklyProposalLesson> Lessons, int SessionsBeforeBreak, int MaxDayMinutes, string? ExamFocus, WeeklyProposalMode Mode,
    IReadOnlyDictionary<DayOfWeek, int>? DayOccurrences = null, IReadOnlyDictionary<DayOfWeek, int>? DayBudgets = null);
public sealed record WeeklyProposedSlot(Guid LessonId, DayOfWeek Day, int StartMinute, int DurationMinutes);
/// <summary>NeededMinutes: the unweighted weekly need rounded up to 30 minutes. NoTime: neither a saved nor a proposed card.</summary>
public sealed record WeeklyProposalLessonResult(Guid LessonId, string Name, int ExistingMinutes, int ProposedMinutes, int NeededMinutes, bool NoTime);
/// <summary>CoveredMinutes: Σ min(needed, saved + proposed), what the week covers of the need; ShortageMinutes = NeededMinutes − CoveredMinutes.</summary>
public sealed record WeeklyProposalResult(IReadOnlyList<WeeklyProposedSlot> Slots, IReadOnlyList<WeeklyProposalLessonResult> Lessons,
    int FreeMinutes, int NeededMinutes, int ShortageMinutes, int CoveredMinutes);

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

    /// <summary>The spaced reviews (one per interval of <see cref="WeeklyPlanEngine.SpacedIntervalDays"/>) that fall inside a plan of this many days.</summary>
    public static int ReviewsWithin(int days) => WeeklyPlanEngine.SpacedIntervalDays.Count(x => x < days);

    /// <summary>
    /// A lesson's weekly need: its remaining work plus one <see cref="WeeklyPlanEngine.SpacedMinutes"/> review per finished topic for each
    /// interval inside the plan, divided by the times the least frequent study weekday still comes before the plan ends
    /// (<paramref name="timesPerWeekday"/>, at least 1). A weekly hour on such a day gives the plan at least that many hours, so a week
    /// without shortage fits the annual plan. Near the exam (under a week) every weekday comes once and the need is the whole work.
    /// </summary>
    public static int WeeklyNeed(int workMinutes, int finishedTopics, int days, int timesPerWeekday) =>
        (int)Math.Ceiling((Math.Max(0, workMinutes) + Math.Max(0, finishedTopics) * ReviewsWithin(days) * WeeklyPlanEngine.SpacedMinutes) / (double)Math.Max(1, timesPerWeekday));

    /// <summary>How many times each weekday comes from <paramref name="today"/> to <paramref name="end"/>, both included.</summary>
    public static IReadOnlyDictionary<DayOfWeek, int> Occurrences(DateOnly today, DateOnly end)
    {
        var days = Math.Max(0, end.DayNumber - today.DayNumber + 1);
        return Week.ToDictionary(day => day, day => Enumerable.Range(0, days).Count(i => today.AddDays(i).DayOfWeek == day));
    }

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

    /// <summary>
    /// The plan end depends on the exams of the week that will be applied, and the proposal on the end. Proposes with the end of the
    /// given exams until the proposed week has the same exams (at most <paramref name="attempts"/> times) and returns the end together
    /// with the proposal made with it, so the two always belong together.
    /// </summary>
    public static async Task<(TEnd End, TResult Result)> SettleAsync<TEnd, TResult>(IReadOnlyList<string> codes, Func<IReadOnlyList<string>, Task<TEnd>> end,
        Func<TEnd, TResult> propose, Func<TResult, IReadOnlyList<string>> applied, int attempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            var planEnd = await end(codes);
            var result = propose(planEnd);
            var next = applied(result);
            if (next.SequenceEqual(codes) || attempt >= Math.Max(1, attempts)) return (planEnd, result);
            codes = next;
        }
    }

    public static WeeklyProposalResult Propose(WeeklyProposalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Windows is null || input.Existing is null || input.Lessons is null || input.SessionsBeforeBreak < 1)
            throw new ArgumentException("Haftalık öneri bilgilerini kontrol edin.");
        var fill = input.Mode == WeeklyProposalMode.Fill;
        var existing = fill ? input.Existing.Where(x => x is not null).ToArray() : [];
        var lessons = input.Lessons.Where(x => x is not null).DistinctBy(x => x.LessonId).ToArray();
        var sessions = FreeSessions(Studied(input), existing, input.SessionsBeforeBreak, day => DayCap(input, day));
        // A saved card on a weekday that no longer comes before the plan ends gives the lesson no time (it still occupies its hour).
        var counted = existing.Where(x => input.DayOccurrences?.GetValueOrDefault(x.Day) != 0).ToArray();
        var plans = lessons.Select(lesson => Plan.For(lesson, counted.Where(x => x.LessonId == lesson.LessonId).Sum(x => x.DurationMinutes), input.ExamFocus)).ToArray();
        var counts = Allocate(plans, sessions.Count);
        var placed = Place(sessions, plans, counts, existing);
        var results = plans.Select(plan =>
        {
            var proposed = placed.Where(x => x.LessonId == plan.Lesson.LessonId).Sum(x => x.DurationMinutes);
            return new WeeklyProposalLessonResult(plan.Lesson.LessonId, plan.Lesson.Name, plan.Existing, proposed, plan.Needed, plan.Existing + proposed == 0);
        }).ToArray();
        var needed = results.Sum(x => x.NeededMinutes);
        var covered = results.Sum(x => Math.Min(x.NeededMinutes, x.ExistingMinutes + x.ProposedMinutes));
        return new(placed, results, sessions.Count * SessionMinutes, needed, needed - covered, covered);
    }

    /// <summary>
    /// Paket H4a (review of PR #132, S5): the end of the period the weekly need is spread over: the earlier of finishBy and the settled plan
    /// end (null without a finishBy, when the period is the plan itself).
    /// </summary>
    public static DateOnly? PeriodEnd(DateOnly? finishBy, DateOnly planEnd) => finishBy is { } date ? (date < planEnd ? date : planEnd) : null;

    /// <summary>
    /// Paket H4a (third review of PR #132): settles each wanted tempo date on the date its proposal really uses. <paramref name="run"/>
    /// makes the proposal for a date and returns its settled plan end; when <see cref="PeriodEnd"/> of the two is earlier than the date
    /// (with several exams a short period can drop a lesson and its exam), the proposal is made again for that date, at most
    /// <paramref name="attempts"/> times. A date that does not settle, or settles on a day the proposal would refuse (before
    /// <paramref name="earliest"/> and not the plan end), is left out; dates that end up equal are merged into the first (most relaxed)
    /// one. Each result is the run for its final date, so it is exactly the proposal the wizard gets for that date.
    /// </summary>
    public static async Task<IReadOnlyList<(T Tag, DateOnly FinishBy, TResult Result)>> TempoDatesAsync<T, TResult>(
        IEnumerable<(T Tag, DateOnly FinishBy)> wanted, DateOnly earliest, DateOnly planEnd, Func<DateOnly, Task<(DateOnly End, TResult Result)>> run,
        int attempts = 3)
    {
        var options = new List<(T Tag, DateOnly FinishBy, TResult Result)>();
        foreach (var (tag, finishBy) in wanted)
        {
            var date = finishBy;
            (DateOnly Date, TResult Result)? settled = null;
            for (var attempt = 1; attempt <= Math.Max(1, attempts); attempt++)
            {
                var (end, result) = await run(date);
                var used = PeriodEnd(date, end)!.Value;
                if (used == date) { settled = (date, result); break; }
                if (used < earliest && used != planEnd) break;
                date = used;
            }
            if (settled is { } done && options.All(x => x.FinishBy != done.Date)) options.Add((tag, done.Date, done.Result));
        }
        return options;
    }

    /// <summary>The windows on weekdays that still come before the plan ends (saved and teacher windows never overlap).</summary>
    private static WeeklyProposalWindow[] Studied(WeeklyProposalInput input) =>
        input.Windows.Where(x => x is not null && input.DayOccurrences?.GetValueOrDefault(x.Day) != 0).ToArray();

    /// <summary>The most a day may hold: the daily limit, or with day budgets min(budget, limit) and nothing on a day without one.</summary>
    private static int DayCap(WeeklyProposalInput input, DayOfWeek day) =>
        input.DayBudgets is null ? input.MaxDayMinutes : Math.Min(input.MaxDayMinutes, input.DayBudgets.GetValueOrDefault(day));

    /// <summary>
    /// Per lesson: Weighted = need × priority factor × exam focus. The lesson wants ceil(max(need, weighted) ÷ 60) sessions (at least one);
    /// it needs ceil(need ÷ 60) (at least one). In Fill mode its saved minutes count first.
    /// </summary>
    private sealed record Plan(WeeklyProposalLesson Lesson, int Existing, int Need, decimal Weighted, int Needed, int NeedNew, int WantNew)
    {
        public static Plan For(WeeklyProposalLesson lesson, int existing, string? focus)
        {
            var need = Math.Max(0, lesson.NeedMinutes);
            var priority = lesson.Priority is >= 1 and <= 5 ? lesson.Priority : 3;
            var weighted = need * PriorityFactor(priority) * FocusFactor(focus, lesson.ExamCode);
            var needTotal = Math.Max(1, (int)Math.Ceiling(need / (double)SessionMinutes));
            var wantTotal = Math.Max(needTotal, (int)Math.Ceiling(weighted / SessionMinutes));
            int New(int sessions) => Math.Max(0, (int)Math.Ceiling((sessions * SessionMinutes - existing) / (double)SessionMinutes));
            return new(lesson with { Priority = priority }, existing, need, weighted, (int)Math.Ceiling(need / 30d) * 30, New(needTotal), New(wantTotal));
        }
        /// <summary>How long the lesson would take with this many sessions: the larger, the later it finishes.</summary>
        /// <remarks>A lesson with neither saved time nor sessions yet has not started: it comes first (no division by zero).</remarks>
        public decimal Load(int sessions)
        {
            var hours = Existing / (decimal)SessionMinutes + sessions;
            return hours == 0 ? decimal.MaxValue : Weighted / hours;
        }
    }

    /// <summary>
    /// When every lesson's wanted sessions fit, each gets them. Otherwise every lesson with work still needing time gets one (or, with
    /// fewer sessions than such lessons, the ones with the largest weighted need do), then each remaining session goes to the lesson that
    /// would finish latest, up to its need. Only then a lesson without work left gets its one hour, and the rest go up to the wanted
    /// sessions.
    /// </summary>
    private static Dictionary<Guid, int> Allocate(IReadOnlyList<Plan> plans, int capacity)
    {
        var counts = plans.ToDictionary(x => x.Lesson.LessonId, _ => 0);
        if (plans.Sum(x => x.WantNew) <= capacity)
        {
            foreach (var plan in plans) counts[plan.Lesson.LessonId] = plan.WantNew;
            return counts;
        }
        var needing = plans.Where(x => x.NeedNew > 0 && x.Need > 0).OrderByDescending(x => x.Weighted).ThenBy(x => x.Lesson.LessonId).ToArray();
        foreach (var plan in needing.Take(capacity)) counts[plan.Lesson.LessonId] = 1;
        var left = Math.Max(0, capacity - needing.Length);
        void Fill(Func<Plan, bool> who, Func<Plan, int> cap)
        {
            while (left > 0 && plans.Where(x => who(x) && counts[x.Lesson.LessonId] < cap(x))
                       .OrderByDescending(x => x.Load(counts[x.Lesson.LessonId])).ThenBy(x => x.Lesson.Priority).ThenBy(x => x.Lesson.LessonId).FirstOrDefault() is { } latest)
            {
                counts[latest.Lesson.LessonId]++; left--;
            }
        }
        Fill(x => x.Need > 0, x => x.NeedNew);
        Fill(x => x.Need == 0, x => x.NeedNew);
        Fill(_ => true, x => x.WantNew);
        return counts;
    }

    private static int DayIndex(DayOfWeek day) => ((int)day + 6) % 7;

    /// <summary>
    /// Every 60-minute session that fits: on :00/:30 inside the availability windows, clear of saved cards, never making a run of
    /// study (saved or proposed, back to back) longer than <paramref name="sessionsBeforeBreak"/> hours, and within the day's cap.
    /// A piece of free time shorter than an hour stays empty.
    /// </summary>
    private static List<(DayOfWeek Day, int Start)> FreeSessions(IReadOnlyList<WeeklyProposalWindow> windows, IReadOnlyList<WeeklyDraftSlot> existing,
        int sessionsBeforeBreak, Func<DayOfWeek, int> dayCap)
    {
        var result = new List<(DayOfWeek, int)>();
        var longestRun = sessionsBeforeBreak * SessionMinutes;
        foreach (var day in Week)
        {
            var busy = existing.Where(x => x.Day == day).Select(x => (From: x.StartMinute, To: x.StartMinute + x.DurationMinutes)).ToList();
            var used = busy.Sum(x => x.To - x.From);
            var maxDayMinutes = dayCap(day);
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

    /// <summary>
    /// Places each lesson's sessions, the priority-1 and hard lessons first, then the lesson with the most sessions left (then by id).
    /// A lesson takes the free session on the day that carries the least study (saved cards included) where it is not yet and where no
    /// card of its name family comes right before or after; a priority-1 or hard lesson prefers the day's first study time. Only when no
    /// such session is left may a lesson come a second time on a day (the same lesson may then form a longer block), and only when an
    /// hour would otherwise stay empty may a lesson of the same name family come right next to it.
    /// </summary>
    private static List<WeeklyProposedSlot> Place(IReadOnlyList<(DayOfWeek Day, int Start)> sessions, IReadOnlyList<Plan> plans,
        Dictionary<Guid, int> counts, IReadOnlyList<WeeklyDraftSlot> existing)
    {
        var left = new Dictionary<Guid, int>(counts);
        var families = plans.ToDictionary(x => x.Lesson.LessonId, x => Family(x.Lesson.Name));
        var cards = existing.Select(x => (x.LessonId, x.Day, From: x.StartMinute, To: x.StartMinute + x.DurationMinutes, Family: Family(x.LessonName))).ToList();
        var free = sessions.ToList();
        var placed = new List<WeeklyProposedSlot>();
        // Stage 0: a new day and no namesake next to it; 1: the same day again; 2: before an hour is dropped, a namesake next to it too.
        foreach (var stage in new[] { 0, 1, 2 })
            while (true)
            {
                var done = false;
                foreach (var plan in plans.Where(x => left[x.Lesson.LessonId] > 0).OrderBy(x => x.Lesson.Priority == 1 || x.Lesson.Hard ? 0 : 1)
                             .ThenByDescending(x => left[x.Lesson.LessonId]).ThenBy(x => x.Lesson.LessonId))
                {
                    var id = plan.Lesson.LessonId; var family = families[id]; var opener = plan.Lesson.Priority == 1 || plan.Lesson.Hard;
                    bool Next(DayOfWeek day, int start, Func<(Guid LessonId, DayOfWeek Day, int From, int To, string Family), bool> card) =>
                        cards.Any(c => c.Day == day && (c.To == start || c.From == start + SessionMinutes) && card(c));
                    var pick = free.Where(s => (stage == 2 || !Next(s.Day, s.Start, c => c.Family == family && c.LessonId != id))
                            && (stage > 0 || !cards.Any(c => c.Day == s.Day && c.LessonId == id)))
                        .OrderBy(s => cards.Count(c => c.Day == s.Day && c.LessonId == id))
                        .ThenBy(s => Next(s.Day, s.Start, c => c.LessonId == id) ? 1 : 0)
                        .ThenBy(s => Next(s.Day, s.Start, c => c.Family == family && c.LessonId != id) ? 1 : 0)
                        .ThenBy(s => cards.Where(c => c.Day == s.Day).Sum(c => c.To - c.From))
                        .ThenBy(s => opener && !cards.Any(c => c.Day == s.Day && c.From < s.Start) ? 0 : 1)
                        .ThenBy(s => DayIndex(s.Day)).ThenBy(s => s.Start)
                        .Select(s => ((DayOfWeek Day, int Start)?)s).FirstOrDefault();
                    if (pick is not { } session) continue;
                    free.Remove(session); left[id]--;
                    placed.Add(new(id, session.Day, session.Start, SessionMinutes));
                    cards.Add((id, session.Day, session.Start, session.Start + SessionMinutes, family));
                    done = true; break;
                }
                if (!done) break;
            }
        return placed.OrderBy(x => DayIndex(x.Day)).ThenBy(x => x.StartMinute).ToList();
    }
}
