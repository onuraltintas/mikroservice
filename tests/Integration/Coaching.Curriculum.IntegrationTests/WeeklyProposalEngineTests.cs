using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Coaching.Curriculum.IntegrationTests;

// Paket H2: the need-based weekly proposal. The engine is pure, so every rule is checked without a database.
public sealed class WeeklyProposalEngineTests
{
    private static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");
    private static WeeklyProposalLesson Lesson(int n, int need, int priority = 3, bool hard = false, string? name = null, string? exam = null) =>
        new(Id(n), name ?? $"Ders {n}", exam, need, priority, hard);
    private static WeeklyProposalWindow Window(DayOfWeek day, int from, int to) => new(day, from, to);
    private static WeeklyProposalResult Propose(IEnumerable<WeeklyProposalWindow> windows, IEnumerable<WeeklyProposalLesson> lessons,
        bool middle = false, int limit = WeeklyDayLimit.SchoolMinutes, string? focus = null, WeeklyProposalMode mode = WeeklyProposalMode.Fill,
        IEnumerable<WeeklyDraftSlot>? existing = null) =>
        WeeklyProposalEngine.Propose(new([.. windows], [.. existing ?? []], [.. lessons], WeeklyProposalEngine.SessionsBeforeBreak(middle), limit, focus, mode));
    private static WeeklyDraftSlot Card(int lesson, DayOfWeek day, int start, int minutes = 60, string? name = null) =>
        new(Guid.NewGuid(), Id(lesson), name ?? $"Ders {lesson}", null, day, start, minutes);
    private static int[] Starts(WeeklyProposalResult result, DayOfWeek day) => result.Slots.Where(x => x.Day == day).Select(x => x.StartMinute).ToArray();

    [Fact]
    public void FiveHoursForThirteenLessons_NamesTheLessonsWithoutTimeAndTheHoursNeeded()
    {
        // Live case (08.11): five hours a week (an hour each weekday evening) and 13 chosen lessons.
        var lessons = Enumerable.Range(1, 13).Select(n => Lesson(n, 60 + n * 10)).ToArray();
        var result = Propose(Weekdays.Select(day => Window(day, 1140, 1200)), lessons);

        Assert.Equal(5, result.Slots.Count);
        Assert.All(result.Slots, x => Assert.Equal(60, x.DurationMinutes));
        // Fewer sessions than lessons: the five with the largest weighted need get one each; the others are named, not dropped.
        Assert.Equal(Enumerable.Range(9, 5).Select(Id).Order(), result.Slots.Select(x => x.LessonId).Order());
        Assert.Equal(8, result.Lessons.Count(x => x.NoTime));
        Assert.Equal(300, result.FreeMinutes);
        Assert.Equal(lessons.Sum(x => (int)Math.Ceiling(x.NeedMinutes / 30d) * 30), result.NeededMinutes);
        Assert.Equal(result.NeededMinutes - 300, result.ShortageMinutes);
    }

    [Fact]
    public void WithEnoughTimeOnlyTheNeededSessionsAreUsed_SpreadOverTheDays()
    {
        var week = WeeklyProposalEngine.Week.Select(day => Window(day, 1020, 1260)).ToArray();
        var result = Propose(week, [Lesson(1, 120), Lesson(2, 50), Lesson(3, 0)]);

        // 2 + 1 sessions, and a lesson with no work left still keeps one session.
        Assert.Equal(4, result.Slots.Count);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday }, result.Slots.Select(x => x.Day));
        Assert.All(result.Slots, x => Assert.Equal(1020, x.StartMinute));
        Assert.Equal(0, result.ShortageMinutes);
        Assert.Equal(new[] { 120, 60, 60 }, result.Lessons.Select(x => x.ProposedMinutes));
        Assert.Equal(new[] { 120, 60, 0 }, result.Lessons.Select(x => x.NeededMinutes));
        Assert.Equal(21 * 60, result.FreeMinutes);
    }

    [Fact]
    public void BreakAfterEverySessionInMiddleSchool_AndAfterTwoSessionsOtherwise()
    {
        var lessons = Enumerable.Range(1, 6).Select(n => Lesson(n, 600)).ToArray();
        var evening = new[] { Window(DayOfWeek.Monday, 1020, 1260) }; // 17:00–21:00
        Assert.Equal(new[] { 1020, 1080, 1170 }, Starts(Propose(evening, lessons), DayOfWeek.Monday));
        Assert.Equal(new[] { 1020, 1110, 1200 }, Starts(Propose(evening, lessons, middle: true), DayOfWeek.Monday));

        // A saved card right before the window counts toward the run of study.
        var saved = new[] { Card(9, DayOfWeek.Monday, 960) };
        Assert.Equal(new[] { 1020, 1110, 1170 }, Starts(Propose(evening, lessons, existing: saved), DayOfWeek.Monday));
        Assert.Equal(new[] { 1050, 1140 }, Starts(Propose(evening, lessons, middle: true, existing: saved), DayOfWeek.Monday));
    }

    [Fact]
    public void TheDayNeverHoldsMoreThanItsLimit()
    {
        var lessons = Enumerable.Range(1, 12).Select(n => Lesson(n, 900)).ToArray();
        var day = new[] { Window(DayOfWeek.Saturday, 0, 1440) };
        Assert.Equal(WeeklyDayLimit.SchoolMinutes, Propose(day, lessons).Slots.Sum(x => x.DurationMinutes));
        Assert.Equal(WeeklyDayLimit.GraduateMinutes, Propose(day, lessons, limit: WeeklyDayLimit.GraduateMinutes).Slots.Sum(x => x.DurationMinutes));
        // Saved cards (90 minutes) count toward the limit in Fill mode: six more hours fit, a seventh would pass 8 hours.
        var saved = new[] { Card(20, DayOfWeek.Saturday, 60), Card(20, DayOfWeek.Saturday, 120, 30) };
        Assert.Equal(360, Propose(day, lessons, existing: saved).Slots.Sum(x => x.DurationMinutes));
    }

    [Fact]
    public void AFreePieceShorterThanAnHourStaysEmpty()
    {
        var lessons = new[] { Lesson(1, 600), Lesson(2, 600) };
        var result = Propose([Window(DayOfWeek.Monday, 1020, 1110), Window(DayOfWeek.Tuesday, 1035, 1125), Window(DayOfWeek.Wednesday, 1020, 1050)], lessons);
        Assert.All(result.Slots, x => Assert.Equal(60, x.DurationMinutes));
        Assert.Equal(new[] { 1020 }, Starts(result, DayOfWeek.Monday));
        Assert.Equal(new[] { 1050 }, Starts(result, DayOfWeek.Tuesday));
        Assert.Empty(Starts(result, DayOfWeek.Wednesday));
        Assert.Equal(120, result.FreeMinutes);
    }

    [Fact]
    public void TheSameLessonComesOnceADay()
    {
        var result = Propose(Weekdays.Take(3).Select(day => Window(day, 1020, 1140)), [Lesson(1, 180), Lesson(2, 180)]);
        Assert.Equal(6, result.Slots.Count);
        Assert.All(result.Slots.GroupBy(x => x.Day), day => Assert.Equal(day.Count(), day.Select(x => x.LessonId).Distinct().Count()));
    }

    [Fact]
    public void LessonsOfOneNameNeverComeBackToBack()
    {
        Assert.Equal(WeeklyProposalEngine.Family("Matematik (TYT)"), WeeklyProposalEngine.Family("MATEMATİK (AYT)"));
        Assert.Equal("ilk ders", WeeklyProposalEngine.Family("İlk Ders"));
        var lessons = new[] { Lesson(1, 60, name: "Matematik (TYT)", exam: "TYT"), Lesson(2, 60, name: "Matematik (AYT)", exam: "AYT"),
            Lesson(3, 120, name: "Türkçe (TYT)", exam: "TYT") };
        var result = Propose([Window(DayOfWeek.Monday, 1020, 1140), Window(DayOfWeek.Tuesday, 1020, 1140)], lessons);
        Assert.Equal(4, result.Slots.Count);
        var names = lessons.ToDictionary(x => x.LessonId, x => WeeklyProposalEngine.Family(x.Name));
        foreach (var day in result.Slots.GroupBy(x => x.Day))
        {
            var ordered = day.OrderBy(x => x.StartMinute).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                if (ordered[i - 1].StartMinute + 60 == ordered[i].StartMinute)
                    Assert.NotEqual(names[ordered[i - 1].LessonId], names[ordered[i].LessonId]);
        }
        // Next to a saved Matematik card, the other Matematik is not placed either.
        var saved = new[] { Card(1, DayOfWeek.Monday, 960, name: "Matematik (TYT)") };
        var fill = Propose([Window(DayOfWeek.Monday, 1020, 1200)], [lessons[1], Lesson(4, 60)], existing: saved);
        Assert.Equal(2, fill.Slots.Count);
        Assert.Equal(Id(4), fill.Slots.Single(x => x.StartMinute == 1020).LessonId);
        Assert.Equal(Id(2), fill.Slots.Single(x => x.StartMinute == 1110).LessonId);
    }

    [Fact]
    public void PriorityOneAndHardLessonsOpenTheDay()
    {
        var lessons = new[] { Lesson(1, 60), Lesson(2, 60, priority: 1), Lesson(3, 60, hard: true) };
        var result = Propose([Window(DayOfWeek.Monday, 1020, 1140), Window(DayOfWeek.Tuesday, 1020, 1140)], lessons);
        Assert.Equal(4, result.Slots.Count); // priority 1 wants ceil(60 × 1.3 / 60) = 2 sessions
        foreach (var day in result.Slots.GroupBy(x => x.Day))
            Assert.Contains(day.OrderBy(x => x.StartMinute).First().LessonId, new[] { Id(2), Id(3) });
    }

    [Fact]
    public void TheYksFocusMovesTimeBetweenTytAndAyt()
    {
        var lessons = new[] { Lesson(1, 120, name: "Türkçe", exam: "TYT"), Lesson(2, 120, name: "Edebiyat", exam: "AYT") };
        var windows = new[] { Window(DayOfWeek.Monday, 1020, 1140), Window(DayOfWeek.Tuesday, 1020, 1080) };
        int Minutes(WeeklyProposalResult result, int lesson) => result.Lessons.Single(x => x.LessonId == Id(lesson)).ProposedMinutes;
        var tyt = Propose(windows, lessons, focus: "tyt");
        Assert.Equal((120, 60), (Minutes(tyt, 1), Minutes(tyt, 2)));
        var ayt = Propose(windows, lessons, focus: "ayt");
        Assert.Equal((60, 120), (Minutes(ayt, 1), Minutes(ayt, 2)));
        Assert.Equal(1.3m, WeeklyProposalEngine.FocusFactor("tyt", "TYT"));
        Assert.Equal(.75m, WeeklyProposalEngine.FocusFactor("tyt", "AYT"));
        Assert.Equal(1m, WeeklyProposalEngine.FocusFactor("balanced", "TYT"));
        Assert.Equal(1m, WeeklyProposalEngine.FocusFactor("ayt", "LGS"));
    }

    [Fact]
    public void FillKeepsSavedCards_AndALessonWithEnoughTimeGetsNoMore_ReplaceProposesTheWholeWeek()
    {
        var saved = new[] { Card(1, DayOfWeek.Monday, 1020), Card(1, DayOfWeek.Tuesday, 1020) };
        var windows = new[] { Window(DayOfWeek.Monday, 1020, 1260), Window(DayOfWeek.Tuesday, 1020, 1260) };
        var lessons = new[] { Lesson(1, 120), Lesson(2, 60) };

        var fill = Propose(windows, lessons, existing: saved);
        var only = Assert.Single(fill.Slots);
        Assert.Equal(Id(2), only.LessonId);
        Assert.DoesNotContain(fill.Slots, x => saved.Any(s => s.Day == x.Day && x.StartMinute < s.StartMinute + 60 && x.StartMinute + 60 > s.StartMinute));
        Assert.Equal((120, 0), (fill.Lessons[0].ExistingMinutes, fill.Lessons[0].ProposedMinutes));
        Assert.Equal(0, fill.ShortageMinutes);

        var replace = Propose(windows, lessons, mode: WeeklyProposalMode.Replace, existing: saved);
        Assert.Equal((0, 120), (replace.Lessons[0].ExistingMinutes, replace.Lessons[0].ProposedMinutes));
        Assert.Equal(3, replace.Slots.Count);
        Assert.Contains(replace.Slots, x => x.Day == DayOfWeek.Monday && x.StartMinute == 1020);
    }

    [Fact]
    public void TheSameInputGivesTheSameProposal()
    {
        var lessons = Enumerable.Range(1, 7).Select(n => Lesson(n, 30 * n, priority: n % 5 + 1, hard: n % 2 == 0)).ToArray();
        var windows = WeeklyProposalEngine.Week.Select(day => Window(day, 960, 1290)).ToArray();
        var first = Propose(windows, lessons);
        var second = Propose(windows, lessons.Reverse());
        Assert.Equal(first.Slots, second.Slots);
        Assert.Equal(first.Lessons.OrderBy(x => x.LessonId), second.Lessons.OrderBy(x => x.LessonId));
    }
}
