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
    public void BreakAfterEverySessionInMiddleSchool_AndAfterThreeSessionsOtherwise()
    {
        var lessons = Enumerable.Range(1, 6).Select(n => Lesson(n, 600)).ToArray();
        var evening = new[] { Window(DayOfWeek.Monday, 1020, 1260) }; // 17:00–21:00
        Assert.Equal(new[] { 1020, 1080, 1140 }, Starts(Propose(evening, lessons), DayOfWeek.Monday)); // Paket I2: three in a row
        Assert.Equal(new[] { 1020, 1110, 1200 }, Starts(Propose(evening, lessons, middle: true), DayOfWeek.Monday));

        // A saved card right before the window counts toward the run of study.
        var saved = new[] { Card(9, DayOfWeek.Monday, 960) };
        Assert.Equal(new[] { 1020, 1080, 1170 }, Starts(Propose(evening, lessons, existing: saved), DayOfWeek.Monday));
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
        Assert.Equal(Id(2), fill.Slots.Single(x => x.StartMinute == 1080).LessonId); // Paket I2: the third hour in a row, after another lesson
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
    [InlineData(2, 0)]
    [InlineData(6, 0)]
    [InlineData(7, 0)]
    [InlineData(8, 1)]
    public void UnderAWeekTheNeedIsTheRemainingWork(int days, int reviews)
    {
        Assert.Equal(reviews, WeeklyProposalEngine.ReviewsWithin(days));
        Assert.Equal(300 + 2 * reviews * WeeklyPlanEngine.SpacedMinutes, WeeklyProposalEngine.WeeklyNeed(300, 2, days, 1));
    }

    [Fact]
    public void TheNeedIsSpreadOverTheTimesTheLeastFrequentStudyDayComes()
    {
        Assert.Equal(80, WeeklyProposalEngine.WeeklyNeed(300, 2, 30, 4)); // (300 + 2 topics × 1 review (7 days) × 10) ÷ 4
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
        // Three touching windows are one three-hour stretch: three hours in a row (Paket I2).
        var touching = Propose([Window(DayOfWeek.Monday, 1020, 1080), Window(DayOfWeek.Monday, 1080, 1140), Window(DayOfWeek.Monday, 1140, 1200)], lessons);
        Assert.Equal(new[] { 1020, 1080, 1140 }, Starts(touching, DayOfWeek.Monday));
        var overlapping = Propose([Window(DayOfWeek.Monday, 1020, 1110), Window(DayOfWeek.Monday, 1050, 1140)], lessons, middle: true);
        Assert.Equal(new[] { 1020 }, Starts(overlapping, DayOfWeek.Monday));
        Assert.Equal(new[] { 1020, 1080 }, Starts(Propose([Window(DayOfWeek.Monday, 1020, 1110), Window(DayOfWeek.Monday, 1050, 1140)], lessons), DayOfWeek.Monday));
    }

    [Fact]
    public void ASavedCardOutsideTheWindowsStillCountsTowardTheRun()
    {
        // 15:00–18:00 free, a saved card 18:00–19:00 outside any window: 17:00 would make four hours in a row (Paket I2: three may).
        var saved = new[] { Card(9, DayOfWeek.Monday, 1080) };
        var result = Propose([Window(DayOfWeek.Monday, 900, 1080)], Enumerable.Range(1, 4).Select(n => Lesson(n, 600)), existing: saved);
        Assert.Equal(new[] { 900, 960 }, Starts(result, DayOfWeek.Monday));
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

    // Paket H4a: a matching routine's day budgets cap each day at min(budget, daily limit); a day without a budget gets nothing.
    [Fact]
    public void DayBudgetsCapEachDay_ADayWithoutABudgetGetsNothing()
    {
        var week = WeeklyProposalEngine.Week.Select(day => Window(day, 900, 1380)).ToArray();
        var lessons = Enumerable.Range(1, 6).Select(n => Lesson(n, 900)).ToArray();
        var budgets = new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 60, [DayOfWeek.Tuesday] = 150, [DayOfWeek.Saturday] = 600 };
        var result = WeeklyProposalEngine.Propose(new(week, [], lessons, 2, 240, null, WeeklyProposalMode.Replace, null, budgets));
        int Minutes(DayOfWeek day) => result.Slots.Where(x => x.Day == day).Sum(x => x.DurationMinutes);
        // Monday 60; Tuesday 150 leaves two hours (a 30-minute rest stays empty); Saturday min(600, 240); no other day.
        Assert.Equal((60, 120, 240), (Minutes(DayOfWeek.Monday), Minutes(DayOfWeek.Tuesday), Minutes(DayOfWeek.Saturday)));
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Saturday }, result.Slots.Select(x => x.Day).Distinct());
        Assert.Equal(420, result.FreeMinutes);
        // A saved card counts toward its day's budget.
        var fill = WeeklyProposalEngine.Propose(new(week, [Card(1, DayOfWeek.Tuesday, 900, 90)], lessons, 2, 240, null, WeeklyProposalMode.Fill, null, budgets));
        Assert.Equal(60, fill.Slots.Where(x => x.Day == DayOfWeek.Tuesday).Sum(x => x.DurationMinutes));
        // Without budgets nothing changes.
        Assert.Equal(7 * 240, WeeklyProposalEngine.Propose(new(week, [], lessons, 2, 240, null, WeeklyProposalMode.Replace)).FreeMinutes);
        // The guarantees still hold under budgets: a lesson once a day while another day is free, breaks, and the limits.
        AssertWeekRules(result, budgets, 240);
    }

    // Review of PR #132 (S5): the proposal works on (and echoes) the earlier of finishBy and the settled plan end.
    [Fact]
    public void ThePeriodEndsAtTheEarlierOfFinishByAndThePlanEnd()
    {
        Assert.Equal(new DateOnly(2027, 5, 29), WeeklyProposalEngine.PeriodEnd(new DateOnly(2027, 6, 9), new DateOnly(2027, 5, 29)));
        Assert.Equal(new DateOnly(2027, 6, 9), WeeklyProposalEngine.PeriodEnd(new DateOnly(2027, 6, 9), new DateOnly(2027, 6, 12)));
        Assert.Null(WeeklyProposalEngine.PeriodEnd(null, new DateOnly(2027, 6, 12)));
    }

    // Third review of PR #132: a tempo date is settled on the date its proposal really uses; dates that change are run again (at most
    // three times), an unstable one or one the proposal endpoint would refuse is left out, and equal final dates merge into the first.
    [Fact]
    public async Task TempoDatesSettleOnTheDateTheProposalUses_ThenMerge()
    {
        DateOnly D(int day) => new(2027, 6, day);
        var runs = new List<DateOnly>();
        // The proposal for a date works until the settled end the fake gives for it.
        var ends = new Dictionary<DateOnly, DateOnly> { [D(20)] = D(15), [D(15)] = D(15), [D(17)] = D(17), [D(12)] = D(10), [D(10)] = D(9), [D(9)] = D(8), [D(8)] = D(8) };
        Task<(DateOnly End, string Result)> Run(DateOnly date) { runs.Add(date); return Task.FromResult((ends[date], "run " + date.Day)); }

        // Relaxed (20) settles on 15, the date of Balanced: Balanced merges into Relaxed, whose numbers are the run for 15.
        var merged = await WeeklyProposalEngine.TempoDatesAsync(new[] { ("Relaxed", D(20)), ("Balanced", D(15)), ("Intense", D(17)) }, D(7), D(20), Run);
        Assert.Equal(new[] { ("Relaxed", D(15), "run 15"), ("Intense", D(17), "run 17") }, merged);
        // 12 → 10 → 9 → 8 does not settle within three runs and is left out; nothing below the earliest allowed date is run.
        runs.Clear();
        Assert.Empty(await WeeklyProposalEngine.TempoDatesAsync(new[] { ("Intense", D(12)) }, D(7), D(20), Run));
        Assert.Equal(new[] { D(12), D(10), D(9) }, runs);
        runs.Clear();
        Assert.Empty(await WeeklyProposalEngine.TempoDatesAsync(new[] { ("Intense", D(12)) }, D(11), D(20), Run));
        Assert.Equal(new[] { D(12) }, runs);
        // The plan end itself is always allowed, even below the earliest date.
        Assert.Equal(new[] { ("Relaxed", D(8), "run 8") }, await WeeklyProposalEngine.TempoDatesAsync(new[] { ("Relaxed", D(8)) }, D(11), D(8), Run));
    }

    private static void AssertWeekRules(WeeklyProposalResult result, IReadOnlyDictionary<DayOfWeek, int> budgets, int limit)
    {
        foreach (var day in result.Slots.GroupBy(x => x.Day))
        {
            Assert.True(day.Sum(x => x.DurationMinutes) <= Math.Min(limit, budgets.GetValueOrDefault(day.Key)));
            var starts = day.Select(x => x.StartMinute).Order().ToArray();
            for (var i = 2; i < starts.Length; i++) Assert.False(starts[i] - starts[i - 2] == 120, "three hours in a row");
        }
        var days = result.Slots.Select(x => x.Day).Distinct().ToArray();
        foreach (var lesson in result.Slots.GroupBy(x => x.LessonId))
            if (lesson.GroupBy(x => x.Day).Any(x => x.Count() > 1))
                Assert.True(days.All(day => lesson.Any(x => x.Day == day)), "a lesson came twice on a day while another study day was free of it");
    }
    // Live case (09.11, main f96ca0bb): a YKS student, focus Dengeli, all 13 lessons at priority Normal and difficulty Normal, five free
    // hours (Mon–Fri 17:00–18:00). The proposal gave the five hours to the alphabetically first lessons. The ids below run in name order
    // as well (Id(1) is the alphabetically first lesson), so neither the name nor the id may decide who gets an hour.
    private static readonly (string Name, string Exam, int Need)[] Yks =
    [
        ("Biyoloji (AYT)", "AYT", 70), ("Biyoloji (TYT)", "TYT", 55), ("Coğrafya (TYT)", "TYT", 45), ("Din Kültürü (TYT)", "TYT", 30),
        ("Felsefe (TYT/AYT)", "TYT", 40), ("Fizik (AYT)", "AYT", 120), ("Fizik (TYT)", "TYT", 90), ("Kimya (AYT)", "AYT", 110),
        ("Kimya (TYT)", "TYT", 80), ("Matematik (AYT)", "AYT", 210), ("Matematik (TYT)", "TYT", 240), ("Tarih (TYT)", "TYT", 60),
        ("Türkçe (TYT)", "TYT", 150),
    ];
    private static WeeklyProposalLesson[] YksLessons(Func<int, int>? id = null, Func<int, int, int>? need = null, Func<int, int>? priority = null,
        Func<int, bool>? hard = null) =>
        Yks.Select((x, i) => new WeeklyProposalLesson(Id((id ?? (n => n))(i + 1)), x.Name, x.Exam, (need ?? ((_, n) => n))(i, x.Need),
            (priority ?? (_ => 3))(i), (hard ?? (_ => false))(i))).ToArray();
    private static string[] Chosen(WeeklyProposalResult result) => result.Lessons.Where(x => !x.NoTime).Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();
    private static WeeklyProposalResult FiveHours(IEnumerable<WeeklyProposalLesson> lessons, string? focus = "balanced", WeeklyProposalMode mode = WeeklyProposalMode.Replace) =>
        Propose(Weekdays.Select(day => Window(day, 1020, 1080)), lessons, focus: focus, mode: mode);

    [Theory]
    [InlineData(WeeklyProposalMode.Replace)]
    [InlineData(WeeklyProposalMode.Fill)]
    public void FiveHoursForThirteenYksLessons_GoToTheFiveLargestNeeds_NotTheFirstNames(WeeklyProposalMode mode)
    {
        var top = new[] { "Fizik (AYT)", "Kimya (AYT)", "Matematik (AYT)", "Matematik (TYT)", "Türkçe (TYT)" };
        // Ids in name order, in reverse name order, and shuffled: the same five lessons each time.
        foreach (var id in new Func<int, int>[] { n => n, n => 14 - n, n => (n * 5) % 13 + 1 })
        {
            var result = FiveHours(YksLessons(id), mode: mode);
            Assert.Equal(5, result.Slots.Count);
            Assert.Equal(5, result.Slots.Select(x => x.LessonId).Distinct().Count());
            Assert.Equal(top, Chosen(result));
            // The alphabetically first lessons have the smallest needs and stay without time; they are named, not dropped.
            Assert.True(result.Lessons.Single(x => x.Name == "Din Kültürü (TYT)").NoTime);
            Assert.True(result.Lessons.Single(x => x.Name == "Biyoloji (AYT)").NoTime);
            Assert.Equal(8, result.Lessons.Count(x => x.NoTime));
        }
    }

    [Fact]
    public void FiveHoursForThirteenYksLessons_TheAlphabeticallyFirstLessonWithTheSmallestNeedNeverWins()
    {
        // Biyoloji (AYT), first by name and by id, now has the smallest need of all.
        var result = FiveHours(YksLessons(need: (i, n) => i == 0 ? 15 : n));
        Assert.True(result.Lessons.Single(x => x.Name == "Biyoloji (AYT)").NoTime);
        Assert.False(result.Lessons.Single(x => x.Name == "Matematik (TYT)").NoTime);
        Assert.Equal(new[] { "Fizik (AYT)", "Kimya (AYT)", "Matematik (AYT)", "Matematik (TYT)", "Türkçe (TYT)" }, Chosen(result));
    }

    [Fact]
    public void UnderShortageThePriorityTheDifficultyAndTheExamFocusChangeWhichLessonsGetTheHours()
    {
        // Priority 1 (×1.3) lifts Fizik (TYT) 90 → 117 above Kimya (AYT) 110.
        var priority = FiveHours(YksLessons(priority: i => Yks[i].Name == "Fizik (TYT)" ? 1 : 3));
        Assert.Equal(new[] { "Fizik (AYT)", "Fizik (TYT)", "Matematik (AYT)", "Matematik (TYT)", "Türkçe (TYT)" }, Chosen(priority));

        // A hard lesson (×1.5, plan H2): Fizik (TYT) 90 → 135, above Fizik (AYT) 120 and Kimya (AYT) 110.
        var hard = FiveHours(YksLessons(hard: i => Yks[i].Name == "Fizik (TYT)"));
        Assert.Equal(new[] { "Fizik (AYT)", "Fizik (TYT)", "Matematik (AYT)", "Matematik (TYT)", "Türkçe (TYT)" }, Chosen(hard));
        // Without the weight, the hard lesson's own need (90) stays below Kimya (AYT) 110.
        Assert.True(FiveHours(YksLessons()).Lessons.Single(x => x.Name == "Fizik (TYT)").NoTime);

        // TYT first (×1.3 TYT, ×0.75 AYT): Fizik (TYT) 117, Kimya (TYT) 104 and Türkçe 195 pass Fizik (AYT) 90 and Kimya (AYT) 82.5.
        var tyt = FiveHours(YksLessons(), focus: "tyt");
        Assert.Equal(new[] { "Fizik (TYT)", "Kimya (TYT)", "Matematik (AYT)", "Matematik (TYT)", "Türkçe (TYT)" }, Chosen(tyt));

        // AYT first (×1.3 AYT, ×0.75 TYT), Biyoloji (AYT) at 90: its 117 passes Türkçe (TYT) 150 → 112.5.
        var ayt = FiveHours(YksLessons(need: (i, n) => Yks[i].Name == "Biyoloji (AYT)" ? 90 : n), focus: "ayt");
        Assert.Equal(new[] { "Biyoloji (AYT)", "Fizik (AYT)", "Kimya (AYT)", "Matematik (AYT)", "Matematik (TYT)" }, Chosen(ayt));
    }

    // The live week above was not made by this engine: before Paket H2 the wizard ranked the courses by priority and then by name and
    // saved that week. Fill keeps saved cards (plan H2, item 4), so with no free hour left it proposes nothing and names the lessons
    // without time; Replace ("Haftanı baştan öner") gives the five hours by need.
    [Fact]
    public void ASavedWeekInNameOrderIsKeptByFill_ReplaceGivesTheHoursByNeed()
    {
        var lessons = YksLessons();
        var saved = Weekdays.Select((day, i) => Card(i + 1, day, 1020, name: Yks[i].Name)).ToArray();
        var fill = Propose(Weekdays.Select(day => Window(day, 1020, 1080)), lessons, focus: "balanced", existing: saved);
        Assert.Empty(fill.Slots);
        Assert.Equal(Yks.Take(5).Select(x => x.Name).Order(StringComparer.Ordinal), Chosen(fill));
        Assert.True(fill.Lessons.Single(x => x.Name == "Matematik (TYT)").NoTime);
        Assert.True(fill.ShortageMinutes > 0);

        var replace = Propose(Weekdays.Select(day => Window(day, 1020, 1080)), lessons, focus: "balanced", mode: WeeklyProposalMode.Replace, existing: saved);
        Assert.Equal(new[] { "Fizik (AYT)", "Kimya (AYT)", "Matematik (AYT)", "Matematik (TYT)", "Türkçe (TYT)" }, Chosen(replace));
    }

    // Review of PR #136 (4): the difficulty weight changes the ranking and the surplus hours, never the need or the shortage.
    [Fact]
    public void TheDifficultyWeightLeavesTheNeedAndTheShortageAlone()
    {
        foreach (var hours in new[] { 1, 3, 10 })
        {
            var windows = WeeklyProposalEngine.Week.Take(hours).Select(day => Window(day, 1020, 1080));
            var plain = Propose(windows, [Lesson(1, 90), Lesson(2, 120), Lesson(3, 45)]);
            var hard = Propose(windows, [Lesson(1, 90, hard: true), Lesson(2, 120), Lesson(3, 45, hard: true)]);
            Assert.Equal((plain.NeededMinutes, plain.ShortageMinutes, plain.FreeMinutes), (hard.NeededMinutes, hard.ShortageMinutes, hard.FreeMinutes));
            Assert.Equal(plain.Lessons.Select(x => x.NeededMinutes), hard.Lessons.Select(x => x.NeededMinutes));
        }
    }

    [Fact]
    public void WithTimeToSpareAHardLessonWantsHalfAgainItsNeed()
    {
        var week = WeeklyProposalEngine.Week.Select(day => Window(day, 1020, 1260));
        // Need 120: two sessions; hard: 120 × 1.5 = 180, three sessions. The need (and so the shortage) stays 120.
        var plain = Propose(week, [Lesson(1, 120)]).Lessons.Single();
        var hard = Propose(week, [Lesson(1, 120, hard: true)]).Lessons.Single();
        Assert.Equal((120, 120), (plain.ProposedMinutes, plain.NeededMinutes));
        Assert.Equal((180, 120), (hard.ProposedMinutes, hard.NeededMinutes));
        // With priority 1 too: 120 × 1.3 × 1.5 = 234 → four sessions.
        Assert.Equal(240, Propose(week, [Lesson(1, 120, priority: 1, hard: true)]).Lessons.Single().ProposedMinutes);
    }

    // Review of PR #136 (1): a lesson without time counts for "the week by need" only when the Replace week gives it time and it
    // still has a need.
    [Fact]
    public void RebuildGainsAreTheLessonsWithANeedThatOnlyReplaceGivesTime()
    {
        var hour = new[] { Window(DayOfWeek.Monday, 1020, 1080) };
        var lessons = new[] { Lesson(1, 60), Lesson(2, 210) };
        var legacy = Propose(hour, lessons, existing: [Card(1, DayOfWeek.Monday, 1020)]);
        var replace = Propose(hour, lessons, mode: WeeklyProposalMode.Replace);
        Assert.Equal(new[] { Id(2) }, WeeklyProposalEngine.RebuildGains(legacy, replace));
        var byNeed = Propose(hour, lessons, existing: [Card(2, DayOfWeek.Monday, 1020)]);
        Assert.Empty(WeeklyProposalEngine.RebuildGains(byNeed, replace));

        // Lesson 2 has nothing left to study: Replace gives it the spare hour, but that is no reason to rebuild the week.
        var two = new[] { Window(DayOfWeek.Monday, 1020, 1080), Window(DayOfWeek.Tuesday, 1020, 1080) };
        var done = new[] { Lesson(1, 60), Lesson(2, 0) };
        var fill = Propose(two, done, existing: [Card(1, DayOfWeek.Monday, 1020), Card(1, DayOfWeek.Tuesday, 1020)]);
        var again = Propose(two, done, mode: WeeklyProposalMode.Replace);
        Assert.True(fill.Lessons.Single(x => x.LessonId == Id(2)).NoTime);
        Assert.Equal(60, again.Lessons.Single(x => x.LessonId == Id(2)).ProposedMinutes);
        Assert.Empty(WeeklyProposalEngine.RebuildGains(fill, again));
    }

    [Fact]
    public void SpacedReviewsFollowTheStudentsChoice()
    {
        Assert.Equal(300, WeeklyProposalEngine.WeeklyNeed(300, 2, 60, 1, Array.Empty<int>()));
        Assert.Equal(300 + 2 * 3 * WeeklyPlanEngine.SpacedMinutes, WeeklyProposalEngine.WeeklyNeed(300, 2, 60, 1, new[] { 7, 30, 45 }));
        // A review that would fall after the plan ends is not counted.
        Assert.Equal(300 + 2 * 1 * WeeklyPlanEngine.SpacedMinutes, WeeklyProposalEngine.WeeklyNeed(300, 2, 20, 1, new[] { 7, 30, 45 }));
        Assert.Equal(new[] { 30 }, WeeklyPlanEngine.RulesFor(new[] { 30 }).SpacedIntervalDays);
        Assert.Equal(WeeklyPlanEngine.DefaultSpacedDays, WeeklyPlanEngine.RulesFor(null).SpacedIntervalDays);

        var preferences = Coaching.Domain.Entities.StudyPlanPreferences.Create(Guid.NewGuid());
        Assert.Null(preferences.SpacedReviewDayList());
        preferences.SetSpacedReviewDays(new[] { 45, 7, 7 });
        Assert.Equal("7,45", preferences.SpacedReviewDays);
        preferences.SetSpacedReviewDays(Array.Empty<int>());
        Assert.Empty(preferences.SpacedReviewDayList()!);
        Assert.Throws<ArgumentException>(() => preferences.SetSpacedReviewDays(new[] { 14 }));
    }

    [Fact]
    public void BackToBackOutcomesOfOneTopicAreOneSessionUpToAnHour()
    {
        var day = new DateOnly(2026, 11, 16); var lesson = Guid.NewGuid(); var topic = Guid.NewGuid(); var other = Guid.NewGuid();
        WeeklyOutcomeSlice Piece(Guid topicId, int start, int minutes, string kind = "Learn", DateOnly? date = null) =>
            new(date ?? day, start, lesson, "Tarih", new WeeklyOutcomeWork(topicId, "Konu", Guid.NewGuid(), "K", "Kazanım", Guid.NewGuid(), 1, minutes), minutes, false, kind, null, 2);
        // 9+9+9+9+9+7 = 52 fits one hour; the next 9 would pass it and opens a new session.
        var pieces = new[] { Piece(topic, 540, 9), Piece(topic, 549, 9), Piece(topic, 558, 9), Piece(topic, 567, 9), Piece(topic, 576, 9), Piece(topic, 585, 7),
            Piece(topic, 592, 9), Piece(other, 601, 20), Piece(other, 630, 20), Piece(other, 650, 10, "Spaced"), Piece(other, 660, 20, date: day.AddDays(1)) };

        var sessions = WeeklyOutcomeScheduler.Sessions(pieces, WeeklyPlanEngine.DefaultTopicSessionMinutes);

        // One topic's hour, its rest, another topic, the same topic after a gap, a spaced review, the next day.
        Assert.Equal(new[] { 6, 1, 1, 1, 1, 1 }, sessions.Select(x => x.Count).ToArray());
        Assert.Equal(new[] { 52, 9, 20, 20, 10, 20 }, sessions.Select(x => x.Sum(p => p.Minutes)).ToArray());
        Assert.Equal(pieces, sessions.SelectMany(x => x).ToArray());
        Assert.All(WeeklyOutcomeScheduler.Sessions(pieces, 0), x => Assert.Single(x));
    }
}
