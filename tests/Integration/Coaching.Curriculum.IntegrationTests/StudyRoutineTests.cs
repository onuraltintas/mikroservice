using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>
/// H3 (user decisions K1, K2, 08.11.2026): the "günlük düzen" answers are checked and turned into free-hour windows with the rules
/// of the wizard the plan is based on: after school until the latest end (öğlenci: an optional morning window ending 1,5 hours
/// before school and the afternoon from an hour after school), no-school days 12:30–14:00 off, busy days start later or are off,
/// dinner and the weekend lunch are cut out, and pieces under an hour (one session, K1) are dropped.
/// </summary>
public sealed class StudyRoutineTests
{
    private static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    // Sabahçı 08:00–14:00, starts at 15:00, until 22:00, dinner 19:00–20:00, weekend 10:00–21:00.
    internal static StudyRoutineAnswers Morning() => new(StudySchoolShift.Morning, 480, 840, 900, 1320, false, null, null, 1140, 1200,
        [], null, 180, StudyWeekendDays.Both, 600, 1260, 240, null);

    private static (DayOfWeek, int, int)[] Windows(StudyRoutineAnswers answers, DayOfWeek day) =>
        StudyRoutine.ToWindows(answers).Windows.Where(x => x.Day == day).Select(x => (x.Day, x.StartMinute, x.EndMinute)).ToArray();

    private static (DayOfWeek, int, int)[] W(params (DayOfWeek, int, int)[] windows) => windows;

    private static StudyRoutineAnswers Valid(StudyRoutineAnswers answers, bool yks = false) => StudyRoutine.Validate(answers, yks);

    private static string Rejected(StudyRoutineAnswers? answers, bool yks = false) =>
        Assert.Throws<StudyPlanValidationException>(() => StudyRoutine.Validate(answers, yks)).Message;

    [Fact]
    public void MorningAndFullDayStudyFromAfterSchoolToTheLatestEndWithoutDinner()
    {
        foreach (var answers in new[] { Valid(Morning()), Valid(Morning() with { SchoolShift = StudySchoolShift.FullDay, SchoolEnd = 960, AfterSchoolStart = 1020 }) })
        {
            var start = answers.AfterSchoolStart!.Value;
            foreach (var day in Weekdays)
                Assert.Equal(W((day, start, 1140), (day, 1200, 1320)), Windows(answers, day));
        }
    }

    [Fact]
    public void AfternoonStudiesInTheMorningUntilNinetyMinutesBeforeSchoolAndFromAnHourAfterSchool()
    {
        // Öğlenci 13:00–18:00, morning from 09:00, after school "18:30" becomes 19:00 (an hour after school), until 23:00.
        var answers = Valid(Morning() with { SchoolShift = StudySchoolShift.Afternoon, SchoolStart = 780, SchoolEnd = 1080,
            AfterSchoolStart = 1110, LatestEnd = 1380, MorningStudy = true, MorningStart = 540, DinnerStart = 1200, DinnerEnd = 1230 });
        Assert.Equal(W((DayOfWeek.Monday, 540, 690), (DayOfWeek.Monday, 1140, 1200), (DayOfWeek.Monday, 1230, 1380)), Windows(answers, DayOfWeek.Monday));
        // A later after-school start wins; without the morning question there is no morning window.
        var later = Valid(answers with { AfterSchoolStart = 1230, MorningStudy = false });
        Assert.Equal(W((DayOfWeek.Friday, 1230, 1380)), Windows(later, DayOfWeek.Friday));
        Assert.Null(later.MorningStart);
    }

    [Fact]
    public void AnAfternoonMorningWindowShorterThanAnHourIsRejectedAndNeverProduced()
    {
        // School at 12:00: the morning must end at 10:30; starting at 10:00 leaves 30 minutes.
        var answers = Morning() with { SchoolShift = StudySchoolShift.Afternoon, SchoolStart = 720, SchoolEnd = 1080, AfterSchoolStart = 1110,
            LatestEnd = 1380, MorningStudy = true, MorningStart = 600 };
        Assert.Contains("Sabah çalışman", Rejected(answers));
        Assert.DoesNotContain(Windows(answers, DayOfWeek.Monday), x => x.Item2 < 720);
        Assert.Equal(9 * 60 + 30, Valid(answers with { MorningStart = 570 }).MorningStart);   // 09:30–10:30 is one session
        Assert.Contains((DayOfWeek.Monday, 570, 630), Windows(answers with { MorningStart = 570 }, DayOfWeek.Monday));
        // A morning start after the window's end gives nothing rather than a broken window.
        Assert.DoesNotContain(Windows(answers with { MorningStart = 660 }, DayOfWeek.Monday), x => x.Item2 < 720);
    }

    [Fact]
    public void NoSchoolDaysBreakFromHalfPastTwelveToTwo()
    {
        var answers = Valid(Morning() with { SchoolShift = StudySchoolShift.NoSchool, SchoolStart = 480, SchoolEnd = 840, AfterSchoolStart = 900, DayStart = 540 });
        Assert.Null(answers.SchoolStart); Assert.Null(answers.AfterSchoolStart);
        Assert.Equal(W((DayOfWeek.Tuesday, 540, 750), (DayOfWeek.Tuesday, 840, 1140), (DayOfWeek.Tuesday, 1200, 1320)), Windows(answers, DayOfWeek.Tuesday));
        // Starting at 12:00 leaves 30 minutes before the break: dropped.
        Assert.Equal(W((DayOfWeek.Tuesday, 840, 1140), (DayOfWeek.Tuesday, 1200, 1320)), Windows(answers with { DayStart = 720 }, DayOfWeek.Tuesday));
        Assert.Contains("06:00 ile 12:00", Rejected(answers with { DayStart = 330 }));
        Assert.Contains("06:00 ile 12:00", Rejected(answers with { DayStart = 750 }));
        Assert.Contains("Güne kaçta", Rejected(answers with { DayStart = null }));
    }

    [Fact]
    public void BusyDaysStartLaterOrHaveNoWindowsAndKeepTheMorningWindow()
    {
        var answers = Valid(Morning() with { BusyDays = [DayOfWeek.Wednesday, DayOfWeek.Monday, DayOfWeek.Monday], BusyStart = 1200 });
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, answers.BusyDays);
        Assert.Equal(W((DayOfWeek.Monday, 1200, 1320)), Windows(answers, DayOfWeek.Monday));
        Assert.Equal(W((DayOfWeek.Tuesday, 900, 1140), (DayOfWeek.Tuesday, 1200, 1320)), Windows(answers, DayOfWeek.Tuesday));
        // "O günler çalışamam": no windows that day, the weekday budget stays.
        var off = Valid(answers with { BusyStart = null });
        Assert.Empty(Windows(off, DayOfWeek.Wednesday));
        Assert.Contains(new StudyDayBudget(DayOfWeek.Wednesday, 180), StudyRoutine.ToWindows(off).DayBudgets);
        // A busy start after the latest end leaves nothing that day.
        Assert.Empty(Windows(Valid(answers with { BusyStart = 1380 }), DayOfWeek.Monday));
        // Öğlenci: the morning window is untouched, even when it ends after noon.
        var afternoon = Valid(Morning() with { SchoolShift = StudySchoolShift.Afternoon, SchoolStart = 840, SchoolEnd = 1080, AfterSchoolStart = 1140,
            LatestEnd = 1380, MorningStudy = true, MorningStart = 600, BusyDays = [DayOfWeek.Monday], BusyStart = 1260 });
        Assert.Equal(W((DayOfWeek.Monday, 600, 750), (DayOfWeek.Monday, 1260, 1380)), Windows(afternoon, DayOfWeek.Monday));
        // No-school busy day: both windows end after noon, so both start at the busy start.
        var noSchool = Valid(Morning() with { SchoolShift = StudySchoolShift.NoSchool, DayStart = 540, BusyDays = [DayOfWeek.Thursday], BusyStart = 960 });
        Assert.Equal(W((DayOfWeek.Thursday, 960, 1140), (DayOfWeek.Thursday, 1200, 1320)), Windows(noSchool, DayOfWeek.Thursday));
        // Busy days are weekdays; without busy days a start is dropped.
        Assert.Contains("pazartesi ile cuma", Rejected(Morning() with { BusyDays = [DayOfWeek.Saturday] }));
        Assert.Null(Valid(Morning() with { BusyStart = 1200 }).BusyStart);
    }

    [Fact]
    public void DinnerIsCutOutWhereverItFalls()
    {
        // Over the window's start, over its end, inside it and over the whole window.
        Assert.Equal(W((DayOfWeek.Monday, 1200, 1320)), Windows(Valid(Morning() with { AfterSchoolStart = 1140 }), DayOfWeek.Monday));
        Assert.Equal(W((DayOfWeek.Monday, 1230, 1320)), Windows(Valid(Morning() with { AfterSchoolStart = 1170, DinnerStart = 1110, DinnerEnd = 1230 }), DayOfWeek.Monday));
        Assert.Equal(W((DayOfWeek.Monday, 900, 1200)), Windows(Valid(Morning() with { LatestEnd = 1260, DinnerStart = 1200, DinnerEnd = 1290 }), DayOfWeek.Monday));
        Assert.Empty(Windows(Valid(Morning() with { AfterSchoolStart = 1200, LatestEnd = 1290, DinnerStart = 1170, DinnerEnd = 1290 }), DayOfWeek.Monday));
        // Dinner leaves two half-hour pieces at both ends: both are dropped.
        Assert.Empty(Windows(Valid(Morning() with { AfterSchoolStart = 1170, LatestEnd = 1320, DinnerStart = 1200, DinnerEnd = 1290 }), DayOfWeek.Monday));
        Assert.Contains("17:00 ile 23:00", Rejected(Morning() with { DinnerStart = 990, DinnerEnd = 1050 }));
        Assert.Contains("17:00 ile 23:00", Rejected(Morning() with { DinnerStart = 1350, DinnerEnd = 1410 }));
        Assert.Contains("en çok 2 saat", Rejected(Morning() with { DinnerStart = 1080, DinnerEnd = 1230 }));
        Assert.Contains("en az yarım saat", Rejected(Morning() with { DinnerStart = 1140, DinnerEnd = 1140 }));
        Assert.Contains("Akşam yemeğinin", Rejected(Morning() with { DinnerEnd = null }));
    }

    [Fact]
    public void TheLatestEndMayBeMidnight()
    {
        var answers = Valid(Morning() with { LatestEnd = 1440 });
        Assert.Equal(W((DayOfWeek.Monday, 900, 1140), (DayOfWeek.Monday, 1200, 1440)), Windows(answers, DayOfWeek.Monday));
        Assert.Contains("18:00 ile 24:00", Rejected(Morning() with { LatestEnd = 1050 }));
        Assert.Contains("tam saat ya da buçuk", Rejected(Morning() with { LatestEnd = 1470 }));
        Assert.Contains("en az 1 saat sonra", Rejected(Morning() with { AfterSchoolStart = 1110, LatestEnd = 1140 }));
    }

    [Fact]
    public void WeekendDaysSkipLunchAndDinnerAndOnlyChosenDaysGetBudgets()
    {
        var both = StudyRoutine.ToWindows(Valid(Morning()));
        foreach (var day in new[] { DayOfWeek.Saturday, DayOfWeek.Sunday })
            Assert.Equal(W((day, 600, 780), (day, 840, 1140), (day, 1200, 1260)), both.Windows.Where(x => x.Day == day).Select(x => (x.Day, x.StartMinute, x.EndMinute)));
        Assert.Equal(new StudyDayBudget[] { new(DayOfWeek.Sunday, 240), new(DayOfWeek.Monday, 180), new(DayOfWeek.Tuesday, 180), new(DayOfWeek.Wednesday, 180),
            new(DayOfWeek.Thursday, 180), new(DayOfWeek.Friday, 180), new(DayOfWeek.Saturday, 240) }, both.DayBudgets);

        var saturday = StudyRoutine.ToWindows(Valid(Morning() with { Weekend = StudyWeekendDays.Saturday, WeekendStart = 780, WeekendEnd = 900 }));
        Assert.Equal(W((DayOfWeek.Saturday, 840, 900)), saturday.Windows.Where(x => x.Day >= DayOfWeek.Saturday || x.Day == DayOfWeek.Sunday)
            .Select(x => (x.Day, x.StartMinute, x.EndMinute)));
        Assert.DoesNotContain(saturday.DayBudgets, x => x.Day == DayOfWeek.Sunday);
        Assert.Single(StudyRoutine.ToWindows(Valid(Morning() with { Weekend = StudyWeekendDays.Sunday })).DayBudgets, x => x.Day is DayOfWeek.Sunday or DayOfWeek.Saturday);

        var none = Valid(Morning() with { Weekend = StudyWeekendDays.None });
        Assert.Null(none.WeekendStart); Assert.Null(none.WeekendMinutes);
        Assert.DoesNotContain(StudyRoutine.ToWindows(none).Windows, x => x.Day is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Equal(5, StudyRoutine.ToWindows(none).DayBudgets.Count);

        Assert.Contains("en az 1 saat sonra", Rejected(Morning() with { WeekendEnd = 630 }));
        Assert.Contains("Hafta sonu günde", Rejected(Morning() with { WeekendMinutes = null }));
        Assert.Contains("Hafta sonu hangi", Rejected(Morning() with { Weekend = null }));
        Assert.Contains("Hafta sonu kaçta", Rejected(Morning() with { WeekendStart = null }));
    }

    [Fact]
    public void SchoolHoursAndDailyMinutesAreChecked()
    {
        Assert.Contains("Okul düzenini", Rejected(Morning() with { SchoolShift = null }));
        Assert.Contains("Günlük düzen", Rejected(null));
        Assert.Contains("Okulun kaçta başladığını", Rejected(Morning() with { SchoolStart = null }));
        Assert.Contains("Okulun kaçta bittiğini", Rejected(Morning() with { SchoolEnd = null }));
        Assert.Contains("tam saat ya da buçuk", Rejected(Morning() with { SchoolStart = 485 }));
        Assert.Contains("en az 3 saat", Rejected(Morning() with { SchoolEnd = 630 }));
        Assert.Contains("Okuldan sonra kaçta", Rejected(Morning() with { AfterSchoolStart = null }));
        Assert.Contains("en az yarım saat sonra", Rejected(Morning() with { AfterSchoolStart = 840 }));
        Assert.Equal(870, Valid(Morning() with { AfterSchoolStart = 870 }).AfterSchoolStart);
        Assert.Contains("Sabah kaçta", Rejected(Morning() with { SchoolShift = StudySchoolShift.Afternoon, SchoolStart = 780, SchoolEnd = 1080, AfterSchoolStart = 1140, MorningStudy = true }));
        Assert.False(Valid(Morning() with { MorningStudy = true, MorningStart = 360 }).MorningStudy);   // only for öğlenci
        foreach (var minutes in new int?[] { null, 30, 630, 75 })
            Assert.Contains(minutes is null ? "Hafta içi günde" : "1 ile 10 saat", Rejected(Morning() with { WeekdayMinutes = minutes }));
        Assert.Equal(600, Valid(Morning() with { WeekdayMinutes = 600 }).WeekdayMinutes);
    }

    [Fact]
    public void ExamFocusIsOnlyForYks()
    {
        Assert.Equal(StudyExamFocus.Ayt, Valid(Morning() with { ExamFocus = StudyExamFocus.Ayt }, yks: true).ExamFocus);
        Assert.Contains("yalnız YKS", Rejected(Morning() with { ExamFocus = StudyExamFocus.Tyt }));
        Assert.Null(Valid(Morning(), yks: true).ExamFocus);
    }

    [Fact]
    public void EveryValidRoutineGivesSortedNonOverlappingSessionLongWindows()
    {
        var random = new Random(20261120);
        int Half(int from, int to) => from + random.Next((to - from) / 30 + 1) * 30;
        var accepted = 0;
        for (var i = 0; i < 4000; i++)
        {
            var schoolStart = Half(360, 840);
            var candidate = new StudyRoutineAnswers((StudySchoolShift)random.Next(4), schoolStart, Half(schoolStart, 1080),
                Half(600, 1260), Half(1050, 1440), random.Next(2) == 0, Half(300, 720), Half(330, 750), Half(1020, 1260),
                null, Weekdays.Where(_ => random.Next(3) == 0).ToArray(), random.Next(3) == 0 ? null : Half(600, 1440),
                Half(60, 600), (StudyWeekendDays)random.Next(4), Half(360, 1080), Half(600, 1440), Half(60, 600), null);
            candidate = candidate with { DinnerEnd = candidate.DinnerStart + Half(30, 120) };
            StudyRoutineAnswers answers;
            try { answers = StudyRoutine.Validate(candidate, false); } catch (StudyPlanValidationException) { continue; }
            accepted++;
            var result = StudyRoutine.ToWindows(answers);
            var availability = StudyAvailability.Create(Guid.NewGuid());
            availability.ReplaceWindows(result.Windows);   // the availability record accepts them as they are
            Assert.True(result.Windows.Count <= 42);
            for (var w = 0; w < result.Windows.Count; w++)
            {
                var window = result.Windows[w];
                Assert.True(window.EndMinute - window.StartMinute >= StudyRoutine.MinimumWindowMinutes);
                Assert.True(window.StartMinute % 30 == 0 && window.EndMinute % 30 == 0);
                Assert.False(window.StartMinute < answers.DinnerEnd && answers.DinnerStart < window.EndMinute, "a window crosses dinner");
                if (window.Day is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    Assert.False(window.StartMinute < 840 && 780 < window.EndMinute, "a weekend window crosses lunch");
                else
                    Assert.True(window.EndMinute <= answers.LatestEnd);
                if (w > 0)
                {
                    var previous = result.Windows[w - 1];
                    Assert.True(previous.Day < window.Day || previous.Day == window.Day && previous.EndMinute <= window.StartMinute);
                }
            }
        }
        Assert.True(accepted > 300, $"only {accepted} random routines were valid");
    }
}
