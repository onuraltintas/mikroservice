using System.Text.Json;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>
/// H3 (user decisions K1, K2, K5, 08.11.2026): the "günlük düzen" answers are checked and turned into free-hour windows with the
/// rules of the wizard the plan is based on: after school until the latest end (öğlenci: an optional morning window ending 1,5
/// hours before school and the afternoon from an hour after school), no-school days 12:30–14:00 off, busy days start later or are
/// off, dinner and the weekend lunch are cut out, and pieces under an hour (one session, K1) are dropped. Answers that leave no
/// window are refused.
/// </summary>
public sealed class StudyRoutineTests
{
    private static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    // Sabahçı 08:00–14:00, starts at 15:00, until 22:00, dinner 19:00–20:00, weekend 10:00–21:00.
    internal static StudyRoutineAnswers Morning() => new(StudySchoolShift.Morning, 480, 840, 900, 1320, false, null, null, 1140, 1200,
        [], false, null, 180, StudyWeekendDays.Both, 600, 1260, 240, null);

    // Öğlenci 13:00–18:00, morning from 09:00, after school from 19:00 (an hour after school), until 23:00, dinner 20:00–20:30.
    internal static StudyRoutineAnswers Afternoon() => Morning() with { SchoolShift = StudySchoolShift.Afternoon, SchoolStart = 780, SchoolEnd = 1080,
        AfterSchoolStart = 1140, LatestEnd = 1380, MorningStudy = true, MorningStart = 540, DinnerStart = 1200, DinnerEnd = 1230 };

    private static (DayOfWeek, int, int)[] Windows(StudyRoutineAnswers answers, DayOfWeek day) =>
        StudyRoutine.ToWindows(answers).Windows.Where(x => x.Day == day).Select(x => (x.Day, x.StartMinute, x.EndMinute)).ToArray();

    private static (DayOfWeek, int, int)[] W(params (DayOfWeek, int, int)[] windows) => windows;

    private static StudyRoutineAnswers Valid(StudyRoutineAnswers answers, bool yks = false, CoachingEducationStatus? education = null) =>
        StudyRoutine.Validate(answers, yks, education);

    private static string Rejected(StudyRoutineAnswers? answers, bool yks = false, CoachingEducationStatus? education = null) =>
        Assert.Throws<StudyPlanValidationException>(() => StudyRoutine.Validate(answers, yks, education)).Message;

    // Paket H4a: the wizard's tempo (Rahat / Dengeli / Yoğun) is one of the answers; none means Rahat.
    [Fact]
    public void TheTempoIsKeptAndNoTempoMeansRelaxed()
    {
        Assert.Null(Valid(Morning()).Tempo);
        Assert.Equal(StudyTempo.Relaxed, StudyRoutine.TempoOf(Valid(Morning())));
        foreach (var tempo in new[] { StudyTempo.Relaxed, StudyTempo.Balanced, StudyTempo.Intense })
        {
            Assert.Equal(tempo, Valid(Morning() with { Tempo = tempo }).Tempo);
            Assert.Equal(tempo, StudyRoutine.TempoOf(Morning() with { Tempo = tempo }));
        }
        Assert.Equal("Tempo Rahat, Dengeli ya da Yoğun olabilir.", Rejected(Morning() with { Tempo = (StudyTempo)7 }));
        // Stored by name; answers saved before the tempo existed read as no tempo.
        var json = new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
        Assert.Contains("\"Tempo\":\"Intense\"", JsonSerializer.Serialize(Morning() with { Tempo = StudyTempo.Intense }, json));
        var old = JsonSerializer.Serialize(Morning(), json).Replace(",\"Tempo\":null", "");
        Assert.DoesNotContain("Tempo", old);
        Assert.Null(JsonSerializer.Deserialize<StudyRoutineAnswers>(old, json)!.Tempo);
    }

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
        var answers = Valid(Afternoon());
        Assert.Equal(W((DayOfWeek.Monday, 540, 690), (DayOfWeek.Monday, 1140, 1200), (DayOfWeek.Monday, 1230, 1380)), Windows(answers, DayOfWeek.Monday));
        // The stored answer is the window start: less than an hour after school is refused (the converter alone would move it).
        Assert.Equal("Öğlenciysen okul çıkışından en az 1 saat sonrasını seç.", Rejected(Afternoon() with { AfterSchoolStart = 1110 }));
        Assert.Contains((DayOfWeek.Monday, 1140, 1200), Windows(Afternoon() with { AfterSchoolStart = 1110 }, DayOfWeek.Monday));
        // A later start; without the morning question there is no morning window.
        var later = Valid(answers with { AfterSchoolStart = 1230, MorningStudy = false });
        Assert.Equal(W((DayOfWeek.Friday, 1230, 1380)), Windows(later, DayOfWeek.Friday));
        Assert.Null(later.MorningStart);
    }

    [Fact]
    public void TheAfternoonMorningWindowStartsAtSixAndIsAtLeastAnHour()
    {
        // School at 12:00: the morning must end at 10:30; starting at 10:00 leaves 30 minutes.
        var answers = Afternoon() with { SchoolStart = 720, MorningStart = 600 };
        Assert.Contains("Sabah çalışman", Rejected(answers));
        Assert.DoesNotContain(Windows(answers, DayOfWeek.Monday), x => x.Item2 < 720);
        Assert.Equal(570, Valid(answers with { MorningStart = 570 }).MorningStart);   // 09:30–10:30 is one session
        Assert.Contains((DayOfWeek.Monday, 570, 630), Windows(answers with { MorningStart = 570 }, DayOfWeek.Monday));
        // A morning start after the window's end gives nothing rather than a broken window.
        Assert.DoesNotContain(Windows(answers with { MorningStart = 660 }, DayOfWeek.Monday), x => x.Item2 < 720);
        Assert.Equal("Sabah başlama saatin 06:00 ya da daha geç olmalı.", Rejected(answers with { MorningStart = 330 }));
        Assert.Equal(360, Valid(answers with { MorningStart = 360 }).MorningStart);
    }

    [Fact]
    public void SchoolStartFitsTheShift()
    {
        Assert.Contains("Sabahçıysan okulun 06:30 ile 09:00", Rejected(Morning() with { SchoolStart = 360 }));
        Assert.Contains("Sabahçıysan", Rejected(Morning() with { SchoolStart = 570 }));
        Assert.Equal(390, Valid(Morning() with { SchoolStart = 390 }).SchoolStart);
        Assert.Contains("Öğlenciysen okulun 11:00 ile 14:00", Rejected(Afternoon() with { SchoolStart = 630 }));
        Assert.Contains("Öğlenciysen okulun", Rejected(Afternoon() with { SchoolStart = 870, SchoolEnd = 1080 }));
        Assert.Contains("Tam gün okuyorsan okulun 07:00 ile 09:30", Rejected(Morning() with { SchoolShift = StudySchoolShift.FullDay, SchoolStart = 600, SchoolEnd = 960, AfterSchoolStart = 1020 }));
        Assert.Equal(570, Valid(Morning() with { SchoolShift = StudySchoolShift.FullDay, SchoolStart = 570, SchoolEnd = 960, AfterSchoolStart = 1020 }).SchoolStart);
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
    public void BusyDaysStartLaterOrAreOffAndKeepTheMorningWindow()
    {
        var answers = Valid(Morning() with { BusyDays = [DayOfWeek.Wednesday, DayOfWeek.Monday, DayOfWeek.Monday], BusyStart = 1200 });
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, answers.BusyDays);
        Assert.Equal(W((DayOfWeek.Monday, 1200, 1320)), Windows(answers, DayOfWeek.Monday));
        Assert.Equal(W((DayOfWeek.Tuesday, 900, 1140), (DayOfWeek.Tuesday, 1200, 1320)), Windows(answers, DayOfWeek.Tuesday));
        // Busy days need a start unless they are off.
        Assert.Equal("Kurs olan günlerde kaçtan sonra çalışabileceğini seç.", Rejected(answers with { BusyStart = null }));
        // "O günler çalışamam": no windows and no budget that day; a start sent with it is dropped.
        var off = Valid(answers with { BusyDaysOff = true });
        Assert.Null(off.BusyStart);
        Assert.Empty(Windows(off, DayOfWeek.Wednesday));
        var offPlan = StudyRoutine.ToWindows(off);
        Assert.DoesNotContain(offPlan.DayBudgets, x => x.Day is DayOfWeek.Monday or DayOfWeek.Wednesday);
        Assert.Contains(new StudyDayBudget(DayOfWeek.Tuesday, 180), offPlan.DayBudgets);
        Assert.Empty(offPlan.DaysWithoutWindows);
        // Without busy days the off flag and the start are cleared.
        var none = Valid(Morning() with { BusyDaysOff = true, BusyStart = 1200 });
        Assert.False(none.BusyDaysOff); Assert.Null(none.BusyStart);
        // A busy start after the latest end leaves nothing that day: the day keeps its budget and is reported for a warning.
        var late = StudyRoutine.ToWindows(Valid(answers with { BusyStart = 1380 }));
        Assert.DoesNotContain(late.Windows, x => x.Day is DayOfWeek.Monday or DayOfWeek.Wednesday);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, late.DaysWithoutWindows);
        // Öğlenci: the morning window is untouched, even when it ends after noon.
        var afternoon = Valid(Afternoon() with { SchoolStart = 840, MorningStart = 600, BusyDays = [DayOfWeek.Monday], BusyStart = 1260 });
        Assert.Equal(W((DayOfWeek.Monday, 600, 750), (DayOfWeek.Monday, 1260, 1380)), Windows(afternoon, DayOfWeek.Monday));
        // No-school busy day: both windows end after noon, so both start at the busy start.
        var noSchool = Valid(Morning() with { SchoolShift = StudySchoolShift.NoSchool, DayStart = 540, BusyDays = [DayOfWeek.Thursday], BusyStart = 960 });
        Assert.Equal(W((DayOfWeek.Thursday, 960, 1140), (DayOfWeek.Thursday, 1200, 1320)), Windows(noSchool, DayOfWeek.Thursday));
        Assert.Contains("pazartesi ile cuma", Rejected(Morning() with { BusyDays = [DayOfWeek.Saturday], BusyStart = 1200 }));
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
    public void AnswersThatLeaveNoWindowAreRefused()
    {
        // After school 18:00, latest 19:00, dinner 18:00–19:00, no weekend.
        Assert.Equal(StudyRoutine.NoWindowMessage, Rejected(Morning() with { SchoolEnd = 1020, AfterSchoolStart = 1080, LatestEnd = 1140,
            DinnerStart = 1080, DinnerEnd = 1140, Weekend = StudyWeekendDays.None }));
        // Every weekday busy and off, no weekend.
        Assert.Equal(StudyRoutine.NoWindowMessage, Rejected(Morning() with { BusyDays = Weekdays, BusyDaysOff = true, Weekend = StudyWeekendDays.None }));
        // The same weekdays with a weekend still have somewhere to study.
        Assert.Empty(StudyRoutine.ToWindows(Valid(Morning() with { BusyDays = Weekdays, BusyDaysOff = true })).Windows.Where(x => Weekdays.Contains(x.Day)));
        // Decision: weekend days chosen with only the lunch hour (13:00–14:00) are refused on their own, even when the weekdays have
        // windows: the student asked to study on the weekend and would get nothing.
        Assert.Equal(StudyRoutine.NoWeekendWindowMessage, Rejected(Morning() with { WeekendStart = 780, WeekendEnd = 840 }));
        Assert.Equal(StudyRoutine.NoWeekendWindowMessage, Rejected(Morning() with { Weekend = StudyWeekendDays.Sunday, WeekendStart = 1140, WeekendEnd = 1200 }));
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
        Assert.Empty(both.DaysWithoutWindows);

        var saturday = StudyRoutine.ToWindows(Valid(Morning() with { Weekend = StudyWeekendDays.Saturday, WeekendStart = 780, WeekendEnd = 900 }));
        Assert.Equal(W((DayOfWeek.Saturday, 840, 900)), saturday.Windows.Where(x => x.Day is DayOfWeek.Saturday or DayOfWeek.Sunday)
            .Select(x => (x.Day, x.StartMinute, x.EndMinute)));
        Assert.DoesNotContain(saturday.DayBudgets, x => x.Day == DayOfWeek.Sunday);
        Assert.Single(StudyRoutine.ToWindows(Valid(Morning() with { Weekend = StudyWeekendDays.Sunday })).DayBudgets, x => x.Day is DayOfWeek.Sunday or DayOfWeek.Saturday);

        var none = Valid(Morning() with { Weekend = StudyWeekendDays.None });
        Assert.Null(none.WeekendStart); Assert.Null(none.WeekendMinutes);
        Assert.DoesNotContain(StudyRoutine.ToWindows(none).Windows, x => x.Day is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Equal(5, StudyRoutine.ToWindows(none).DayBudgets.Count);

        Assert.Equal("Hafta sonu başlama saatin 07:00 ya da daha geç olmalı.", Rejected(Morning() with { WeekendStart = 390 }));
        Assert.Equal(420, Valid(Morning() with { WeekendStart = 420 }).WeekendStart);
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
        Assert.Contains("Sabah kaçta", Rejected(Afternoon() with { MorningStart = null }));
        Assert.False(Valid(Morning() with { MorningStudy = true, MorningStart = 360 }).MorningStudy);   // only for öğlenci
        foreach (var minutes in new int?[] { null, 30, 630, 75 })
            Assert.Contains(minutes is null ? "Hafta içi günde" : "1 ile 10 saat", Rejected(Morning() with { WeekdayMinutes = minutes }));
    }

    [Fact]
    public void DailyMinutesFollowTheDailyLimitOfTheEducationStatus()
    {
        // K5: 8 hours a day; 10 hours for lise and university graduates (mirrors WeeklyDayLimit from Paket H2).
        foreach (var status in new CoachingEducationStatus?[] { null, CoachingEducationStatus.Unknown, CoachingEducationStatus.Student, CoachingEducationStatus.UniversityStudent })
        {
            Assert.Equal(480, Valid(Morning() with { WeekdayMinutes = 480, WeekendMinutes = 480 }, education: status).WeekdayMinutes);
            Assert.Equal("Hafta içi günlük süren en çok 8 saat olabilir.", Rejected(Morning() with { WeekdayMinutes = 510 }, education: status));
            Assert.Equal("Hafta sonu günlük süren en çok 8 saat olabilir.", Rejected(Morning() with { WeekendMinutes = 600 }, education: status));
        }
        foreach (var status in new[] { CoachingEducationStatus.HighSchoolGraduate, CoachingEducationStatus.UniversityGraduate })
            Assert.Equal((600, 600), (Valid(Morning() with { WeekdayMinutes = 600, WeekendMinutes = 600 }, education: status).WeekdayMinutes!.Value,
                Valid(Morning() with { WeekdayMinutes = 600, WeekendMinutes = 600 }, education: status).WeekendMinutes!.Value));
    }

    [Fact]
    public void ExamFocusIsKeptOnlyForYks()
    {
        Assert.Equal(StudyExamFocus.Ayt, Valid(Morning() with { ExamFocus = StudyExamFocus.Ayt }, yks: true).ExamFocus);
        // A focus left from an earlier YKS purpose does not block saving: it is cleared.
        Assert.Null(Valid(Morning() with { ExamFocus = StudyExamFocus.Tyt }).ExamFocus);
        Assert.Null(Valid(Morning(), yks: true).ExamFocus);
    }

    [Fact]
    public void AnswersTravelThroughTheApiJsonWithNames()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Coaching.API.CoachingApiJson.Configure(options);
        var body = """
            { "expectedVersion": 2, "expectedAvailabilityVersion": 5, "answers": { "schoolShift": "afternoon", "schoolStart": 780,
              "schoolEnd": 1080, "afterSchoolStart": 1140, "latestEnd": 1380, "morningStudy": true, "morningStart": 540,
              "dinnerStart": 1200, "dinnerEnd": 1230, "busyDays": ["Monday", "friday"], "busyDaysOff": false, "busyStart": 1260,
              "weekdayMinutes": 180, "weekend": "Saturday", "weekendStart": 600, "weekendEnd": 900, "weekendMinutes": 120, "examFocus": "Tyt" } }
            """;
        var update = JsonSerializer.Deserialize<StudyRoutineUpdate>(body, options)!;
        Assert.Equal((2, 5), (update.ExpectedVersion!.Value, update.ExpectedAvailabilityVersion!.Value));
        Assert.Equal(StudySchoolShift.Afternoon, update.Answers!.SchoolShift);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Friday }, update.Answers.BusyDays);
        Assert.Equal(StudyExamFocus.Tyt, update.Answers.ExamFocus);
        var written = JsonSerializer.Serialize(update.Answers, options);
        Assert.Contains("\"busyDays\":[\"Monday\",\"Friday\"]", written);
        Assert.Contains("\"schoolShift\":\"Afternoon\"", written);
        Assert.Contains("\"weekend\":\"Saturday\"", written);
        // An unknown name or a number is a JSON error. MVC turns it into the shared 400 "Validation Error" problem details
        // (AddEduPlatformApiConventions), as on every other coaching endpoint; the client shows its own text for it.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StudyRoutineUpdate>(body.Replace("\"afternoon\"", "\"Evening\""), options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StudyRoutineUpdate>(body.Replace("\"friday\"", "\"Someday\""), options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StudyRoutineUpdate>(body.Replace("\"Saturday\"", "1"), options));
    }

    [Fact]
    public void EveryValidRoutineGivesSortedNonOverlappingSessionLongWindows()
    {
        var random = new Random(20261120);
        int Half(int from, int to) => from + random.Next((to - from) / 30 + 1) * 30;
        var accepted = 0;
        for (var i = 0; i < 8000; i++)
        {
            var shift = (StudySchoolShift)random.Next(4);
            var schoolStart = shift == StudySchoolShift.Afternoon ? Half(660, 840) : Half(390, 570);
            var schoolEnd = Half(schoolStart + 180, Math.Max(schoolStart + 180, 1080));
            var candidate = new StudyRoutineAnswers(shift, schoolStart, schoolEnd, Half(schoolEnd + 30, Math.Max(schoolEnd + 30, 1260)),
                Half(1080, 1440), random.Next(2) == 0, Half(360, 720), Half(360, 720), Half(1020, 1260), null,
                Weekdays.Where(_ => random.Next(3) == 0).ToArray(), random.Next(3) == 0, Half(600, 1440),
                Half(60, 480), (StudyWeekendDays)random.Next(4), Half(420, 1080), Half(600, 1440), Half(60, 480), null);
            candidate = candidate with { DinnerEnd = candidate.DinnerStart + Half(30, 120) };
            StudyRoutineAnswers answers;
            try { answers = StudyRoutine.Validate(candidate, false); } catch (StudyPlanValidationException) { continue; }
            accepted++;
            var result = StudyRoutine.ToWindows(answers);
            Assert.NotEmpty(result.Windows);
            var availability = StudyAvailability.Create(Guid.NewGuid());
            availability.ReplaceWindows(result.Windows);   // the availability record accepts them as they are
            Assert.True(result.Windows.Count <= 42);
            Assert.All(result.DaysWithoutWindows, day => Assert.DoesNotContain(result.Windows, x => x.Day == day));
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
        Assert.True(accepted > 500, $"only {accepted} random routines were valid");
    }
}
