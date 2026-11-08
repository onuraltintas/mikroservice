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

    // Review of PR #130 (1a): a course with a saved card on Monday gets its next hour on the free Tuesday, not on Monday again.
    [Fact]
    public void ACourseWithASavedCardGoesToAnotherDay()
    {
        var saved = new[] { Card(1, DayOfWeek.Monday, 960) };
        var result = Propose([Window(DayOfWeek.Monday, 1080, 1140), Window(DayOfWeek.Tuesday, 1080, 1140)], [Lesson(1, 120)], existing: saved);
        var slot = Assert.Single(result.Slots);
        Assert.Equal((DayOfWeek.Tuesday, 1080), (slot.Day, slot.StartMinute));
    }

    // Review of PR #130 (1b): the second Matematik (AYT) hour goes to the free Wednesday, not twice on Monday; Tuesday's only hour
    // would follow the saved Matematik (TYT) card.
    [Fact]
    public void TheSecondHourGoesToAFreeDayBeforeTheSameDayAgain()
    {
        var saved = new[] { Card(1, DayOfWeek.Tuesday, 960, name: "Matematik (TYT)") };
        var result = Propose([Window(DayOfWeek.Monday, 1020, 1080), Window(DayOfWeek.Monday, 1140, 1200), Window(DayOfWeek.Tuesday, 1020, 1080),
            Window(DayOfWeek.Wednesday, 1020, 1080)], [Lesson(2, 120, name: "Matematik (AYT)", exam: "AYT")], existing: saved);
        Assert.Equal(new[] { (DayOfWeek.Monday, 1020), (DayOfWeek.Wednesday, 1020) }, result.Slots.Select(x => (x.Day, x.StartMinute)));
    }

    [Fact]
    public void TheLeastLoadedDaysComeFirstCountingSavedCards_ASecondHourOnADayOnlyWhenNoDayIsLeft()
    {
        // Monday and Tuesday already hold saved study; the two new hours go to Wednesday and Thursday.
        var saved = new[] { Card(9, DayOfWeek.Monday, 900), Card(9, DayOfWeek.Tuesday, 900) };
        var windows = Weekdays.Take(4).Select(day => Window(day, 1020, 1080)).ToArray();
        var spread = Propose(windows, [Lesson(1, 60), Lesson(2, 60)], existing: saved);
        Assert.Equal(new[] { DayOfWeek.Wednesday, DayOfWeek.Thursday }, spread.Slots.Select(x => x.Day));
        // One day only: the course comes twice rather than losing time.
        var oneDay = Propose([Window(DayOfWeek.Friday, 960, 1260)], [Lesson(1, 120)]);
        Assert.Equal(2, oneDay.Slots.Count);
    }

    // Review of PR #130 (6): a day whose saved cards come before the free hour has no "first session" left.
    [Fact]
    public void TheFirstSessionOfTheDayCountsSavedCards()
    {
        var saved = new[] { Card(9, DayOfWeek.Monday, 900), Card(9, DayOfWeek.Tuesday, 1200) };
        var result = Propose([Window(DayOfWeek.Monday, 1020, 1080), Window(DayOfWeek.Tuesday, 1080, 1140)],
            [Lesson(1, 60), Lesson(2, 60, priority: 1)], existing: saved);
        Assert.Equal(Id(2), result.Slots.Single(x => x.Day == DayOfWeek.Tuesday).LessonId);
        Assert.Equal(Id(1), result.Slots.Single(x => x.Day == DayOfWeek.Monday).LessonId);
    }

    // Review of PR #130 (2): a course without work left gets its hour only after the courses with work reach their need.
    [Fact]
    public void ACourseWithoutWorkWaitsUntilTheOthersHaveTheirNeed()
    {
        var windows = new[] { Window(DayOfWeek.Monday, 1020, 1080), Window(DayOfWeek.Tuesday, 1020, 1080), Window(DayOfWeek.Wednesday, 1020, 1080) };
        var lessons = new[] { Lesson(1, 120), Lesson(2, 0) };
        var tight = Propose(windows.Take(2), lessons);
        Assert.Equal((120, 0, true), (tight.Lessons[0].ProposedMinutes, tight.Lessons[1].ProposedMinutes, tight.Lessons[1].NoTime));
        Assert.Equal((0, 120, 120), (tight.ShortageMinutes, tight.CoveredMinutes, tight.NeededMinutes));
        var enough = Propose(windows, lessons);
        Assert.Equal((120, 60), (enough.Lessons[0].ProposedMinutes, enough.Lessons[1].ProposedMinutes));
    }

    // Review of PR #130 (2): "şu an" is what the week covers of the need, so it never reaches the need while there is a shortage.
    [Fact]
    public void CoveredMinutesCountOnlyUpToEachCoursesNeed()
    {
        // One course has three saved hours for a one-hour need, the other has none for two: 3 hours planned for 3 needed, yet
        // only 1 hour of the need is covered.
        var saved = new[] { Card(1, DayOfWeek.Monday, 900), Card(1, DayOfWeek.Tuesday, 900), Card(1, DayOfWeek.Wednesday, 900) };
        var result = Propose([], [Lesson(1, 60), Lesson(2, 120)], existing: saved);
        Assert.Equal((180, 120, 60), (result.NeededMinutes, result.ShortageMinutes, result.CoveredMinutes));
        Assert.Equal(result.NeededMinutes - result.ShortageMinutes, result.CoveredMinutes);
        Assert.True(result.Lessons.Sum(x => x.ExistingMinutes + x.ProposedMinutes) >= result.NeededMinutes);
    }

    // Review of PR #130 (3): close to the exam the weekly need is the remaining work, never work × 7.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 1)]
    public void UnderAWeekTheNeedIsTheRemainingWork(int days, int reviews)
    {
        Assert.Equal(reviews, WeeklyProposalEngine.ReviewsWithin(days));
        Assert.Equal(300 + 2 * reviews * WeeklyPlanEngine.SpacedMinutes, WeeklyProposalEngine.WeeklyNeed(300, 2, days, 1));
    }

    [Fact]
    public void TheNeedIsSpreadOverTheTimesTheLeastFrequentStudyDayComes()
    {
        Assert.Equal(90, WeeklyProposalEngine.WeeklyNeed(300, 2, 30, 4)); // (300 + 2 topics × 2 reviews (1 and 7 days) × 15) ÷ 4
        var occurrences = WeeklyProposalEngine.Occurrences(new DateOnly(2027, 5, 24), new DateOnly(2027, 6, 1)); // Monday to the next Tuesday
        Assert.Equal((2, 2, 1, 1), (occurrences[DayOfWeek.Monday], occurrences[DayOfWeek.Tuesday], occurrences[DayOfWeek.Wednesday], occurrences[DayOfWeek.Sunday]));
        // A weekday that does not come before the end gets no session.
        var lessons = new[] { Lesson(1, 600) };
        var shortWeek = WeeklyProposalEngine.Propose(new([Window(DayOfWeek.Monday, 1020, 1080), Window(DayOfWeek.Friday, 1020, 1080)], [], lessons, 2,
            WeeklyDayLimit.SchoolMinutes, null, WeeklyProposalMode.Fill, WeeklyProposalEngine.Occurrences(new DateOnly(2027, 5, 24), new DateOnly(2027, 5, 26))));
        Assert.Equal(DayOfWeek.Monday, Assert.Single(shortWeek.Slots).Day);
    }

    [Fact]
    public void TheLastHourOfTheDayIsUsed_AndTouchingOrOverlappingWindowsAreOneStretch()
    {
        var late = Propose([Window(DayOfWeek.Monday, 1380, 1440)], [Lesson(1, 60)]);
        Assert.Equal(1380, Assert.Single(late.Slots).StartMinute);
        var lessons = Enumerable.Range(1, 4).Select(n => Lesson(n, 600)).ToArray();
        // 17:00–18:00 and 18:00–19:00 touch: two hours in a row, then a break is needed (19:00–20:00 in a third window stays a run of 3).
        var touching = Propose([Window(DayOfWeek.Monday, 1020, 1080), Window(DayOfWeek.Monday, 1080, 1140), Window(DayOfWeek.Monday, 1140, 1200)], lessons);
        Assert.Equal(new[] { 1020, 1080 }, Starts(touching, DayOfWeek.Monday));
        var overlapping = Propose([Window(DayOfWeek.Monday, 1020, 1110), Window(DayOfWeek.Monday, 1050, 1140)], lessons, middle: true);
        Assert.Equal(new[] { 1020 }, Starts(overlapping, DayOfWeek.Monday));
        Assert.Equal(new[] { 1020, 1080 }, Starts(Propose([Window(DayOfWeek.Monday, 1020, 1110), Window(DayOfWeek.Monday, 1050, 1140)], lessons), DayOfWeek.Monday));
    }

    [Fact]
    public void ASavedCardOutsideTheWindowsStillCountsTowardTheRun()
    {
        // 16:00–18:00 free, a saved card 18:00–19:00 outside any window: 17:00 would make three hours in a row.
        var saved = new[] { Card(9, DayOfWeek.Monday, 1080) };
        var result = Propose([Window(DayOfWeek.Monday, 960, 1080)], Enumerable.Range(1, 3).Select(n => Lesson(n, 600)), existing: saved);
        Assert.Equal(new[] { 960 }, Starts(result, DayOfWeek.Monday));
    }

    // Merge of Paket H1: the daily limit counts saved cards; a day already at or over the limit gets nothing (a draft saved above the
    // limit before Paket H1 keeps its cards).
    [Fact]
    public void ADayAtOrOverTheLimitGetsNoNewHour()
    {
        var full = Enumerable.Range(0, 8).Select(i => Card(9, DayOfWeek.Monday, 480 + i * 60)).ToArray();
        var over = Enumerable.Range(0, 9).Select(i => Card(9, DayOfWeek.Tuesday, 420 + i * 60)).ToArray();
        var result = Propose([Window(DayOfWeek.Monday, 0, 1440), Window(DayOfWeek.Tuesday, 0, 1440), Window(DayOfWeek.Wednesday, 1020, 1080)],
            [Lesson(1, 600)], existing: [.. full, .. over]);
        Assert.Equal(DayOfWeek.Wednesday, Assert.Single(result.Slots).Day);
    }

    // Final review of PR 130 (1): a lesson without work and without saved time used to divide by zero in the shortage branch.
    [Fact]
    public void ALessonWithoutWorkOrSavedTimeDoesNotBreakTheShortageBranch()
    {
        var result = Propose([Window(DayOfWeek.Monday, 1020, 1080), Window(DayOfWeek.Tuesday, 1020, 1080)], [Lesson(1, 60, priority: 1), Lesson(2, 0)]);
        Assert.Equal(2, result.Slots.Count);
        Assert.Equal(0, result.ShortageMinutes);
    }

    [Fact]
    public void SmallRandomWeeksNeverThrowAndKeepTheRules()
    {
        var random = new Random(130);
        for (var run = 0; run < 400; run++)
        {
            var lessons = Enumerable.Range(1, random.Next(1, 6)).Select(n => Lesson(n, random.Next(0, 4) * 60 + random.Next(0, 2) * 30, random.Next(1, 6),
                random.Next(2) == 0, random.Next(3) == 0 ? "Matematik (TYT)" : random.Next(2) == 0 ? "Matematik (AYT)" : null)).ToArray();
            var windows = Enumerable.Range(0, random.Next(0, 5)).Select(_ => { var start = random.Next(0, 44) * 30; return Window(WeeklyProposalEngine.Week[random.Next(7)], start, Math.Min(1440, start + random.Next(1, 8) * 30)); }).ToArray();
            var saved = Enumerable.Range(0, random.Next(0, 3)).Select(i => Card(random.Next(1, 7), WeeklyProposalEngine.Week[random.Next(7)], 60 * (8 + i * 3))).ToArray();
            var mode = random.Next(2) == 0 ? WeeklyProposalMode.Fill : WeeklyProposalMode.Replace;
            var result = WeeklyProposalEngine.Propose(new(windows, saved, lessons, random.Next(1, 3), random.Next(2) == 0 ? 480 : 600,
                new[] { null, "tyt", "balanced", "ayt" }[random.Next(4)], mode));
            Assert.All(result.Slots, x => Assert.True(x.DurationMinutes == 60 && x.StartMinute % 30 == 0 && x.StartMinute + 60 <= 1440));
            Assert.Equal(result.NeededMinutes - result.CoveredMinutes, result.ShortageMinutes);
            Assert.True(result.ShortageMinutes >= 0);
        }
    }

    // Final review of PR 130 (2): before an hour stays empty, a lesson may come right after its namesake.
    [Fact]
    public void NamesakesComeBackToBackRatherThanLeavingAnHourEmpty()
    {
        var result = Propose([Window(DayOfWeek.Monday, 960, 1080)], [Lesson(1, 60, name: "Matematik (TYT)", exam: "TYT"), Lesson(2, 60, name: "Matematik (AYT)", exam: "AYT")]);
        Assert.Equal(2, result.Slots.Count);
        Assert.Equal(0, result.ShortageMinutes);
    }

    // Final review of PR 130 (8): a saved card on a weekday that does not come before the end gives no time.
    [Fact]
    public void SavedCardsOnWeekdaysThatNoLongerComeDoNotCount()
    {
        var saved = new[] { Card(1, DayOfWeek.Friday, 1020) };
        var monToWed = WeeklyProposalEngine.Occurrences(new DateOnly(2027, 5, 24), new DateOnly(2027, 5, 26));
        var result = WeeklyProposalEngine.Propose(new([Window(DayOfWeek.Monday, 1020, 1080)], saved, [Lesson(1, 60)], 2, 480, null, WeeklyProposalMode.Fill, monToWed));
        Assert.Equal((0, 60, 60, 0), (result.Lessons[0].ExistingMinutes, result.Lessons[0].ProposedMinutes, result.CoveredMinutes, result.ShortageMinutes));
        Assert.Equal(DayOfWeek.Monday, Assert.Single(result.Slots).Day);
    }

    // Final review of PR 130 (7): when the exams keep changing, the end returned is the one the returned proposal was made with.
    [Fact]
    public async Task TheEndReturnedIsTheOneTheProposalWasMadeWith()
    {
        var ends = new List<string>();
        var (end, result) = await WeeklyProposalEngine.SettleAsync<string, string>(["A"], codes => { ends.Add(codes[0]); return Task.FromResult(codes[0]); },
            planEnd => "proposal for " + planEnd, proposal => [proposal.EndsWith('A') ? "B" : "A"]);
        Assert.Equal(3, ends.Count);
        Assert.Equal("proposal for " + end, result);
        var (stable, made) = await WeeklyProposalEngine.SettleAsync<string, string>(["A"], codes => Task.FromResult(codes[0]), planEnd => planEnd, _ => ["A"]);
        Assert.Equal(("A", "A"), (stable, made));
    }
}
