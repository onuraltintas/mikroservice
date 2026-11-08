using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

/// <summary>H3: when the student goes to school (sabahçı, öğlenci, tam gün) or that she does not go to school.</summary>
public enum StudySchoolShift { Morning, Afternoon, FullDay, NoSchool }

/// <summary>H3: the weekend days the student studies on.</summary>
public enum StudyWeekendDays { Both, Saturday, Sunday, None }

/// <summary>H3, user decision K6: a YKS student's TYT/AYT weighting (used by the weekly proposal).</summary>
public enum StudyExamFocus { Tyt, Balanced, Ayt }

/// <summary>
/// H3 (user decision K2): the answers to the "günlük düzen" questions, saved as the student's study routine profile.
/// Every time is a minute of the day on the Europe/Istanbul wall clock (0–1440) on a full or half hour.
/// </summary>
public sealed record StudyRoutineAnswers(
    StudySchoolShift? SchoolShift,
    int? SchoolStart,
    int? SchoolEnd,
    int? AfterSchoolStart,
    int? LatestEnd,
    bool MorningStudy,
    int? MorningStart,
    int? DayStart,
    int? DinnerStart,
    int? DinnerEnd,
    IReadOnlyList<DayOfWeek>? BusyDays,
    int? BusyStart,
    int? WeekdayMinutes,
    StudyWeekendDays? Weekend,
    int? WeekendStart,
    int? WeekendEnd,
    int? WeekendMinutes,
    StudyExamFocus? ExamFocus);

/// <summary>The most a day should get from the weekly proposal (H4 caps a day at min(budget, daily limit)).</summary>
public sealed record StudyDayBudget(DayOfWeek Day, int Minutes);

public sealed record StudyRoutineWindows(IReadOnlyList<StudyAvailabilityWindow> Windows, IReadOnlyList<StudyDayBudget> DayBudgets);

/// <summary>
/// H3: checks the routine answers and turns them into weekly availability windows, with the rules of the wizard the plan is
/// based on. Pure: no database, no clock.
/// </summary>
public static class StudyRoutine
{
    public const int SchemaVersion = 1;
    /// <summary>User decision K1: a study session is 60 minutes, so a shorter free piece is of no use.</summary>
    public const int MinimumWindowMinutes = 60;
    public const int Noon = 720, LunchStart = 780, LunchEnd = 840, NoSchoolMorningEnd = 750;
    private static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>
    /// Checks the answers and returns them with the fields that do not apply cleared. Throws <see cref="StudyPlanValidationException"/>
    /// with a sentence for the student.
    /// </summary>
    public static StudyRoutineAnswers Validate(StudyRoutineAnswers? answers, bool preparesForYks)
    {
        if (answers is null) throw Invalid("Günlük düzen sorularını yanıtla.");
        if (answers.SchoolShift is not { } shift || !Enum.IsDefined(shift)) throw Invalid("Okul düzenini seç.");
        int? schoolStart = null, schoolEnd = null, afterSchool = null, morningStart = null, dayStart = null;
        var morningStudy = false;
        int studyStart;
        if (shift == StudySchoolShift.NoSchool)
        {
            dayStart = Time(answers.DayStart, "Güne kaçta başlayacağını seç.", "Güne başlama saatin");
            if (dayStart < 360 || dayStart > 720) throw Invalid("Güne başlama saatin 06:00 ile 12:00 arasında olmalı.");
            studyStart = dayStart.Value;
        }
        else
        {
            schoolStart = Time(answers.SchoolStart, "Okulun kaçta başladığını seç.", "Okulun giriş saati");
            schoolEnd = Time(answers.SchoolEnd, "Okulun kaçta bittiğini seç.", "Okulun çıkış saati");
            if (schoolEnd - schoolStart < 180) throw Invalid("Okulun çıkış saati, giriş saatinden en az 3 saat sonra olmalı.");
            afterSchool = Time(answers.AfterSchoolStart, "Okuldan sonra kaçta çalışmaya başlayacağını seç.", "Okuldan sonra başlama saatin");
            if (afterSchool < schoolEnd + 30)
                throw Invalid("Okuldan sonra çalışmaya, okulun bitişinden en az yarım saat sonra başlayabilirsin.");
            studyStart = shift == StudySchoolShift.Afternoon ? Math.Max(afterSchool.Value, schoolEnd.Value + 60) : afterSchool.Value;
            if (shift == StudySchoolShift.Afternoon && answers.MorningStudy)
            {
                morningStudy = true;
                morningStart = Time(answers.MorningStart, "Sabah kaçta çalışmaya başlayacağını seç.", "Sabah başlama saatin");
                if (schoolStart.Value - 90 - morningStart < MinimumWindowMinutes)
                    throw Invalid("Sabah çalışman okuldan 1,5 saat önce bitiyor; en az 1 saat çalışabilmen için daha erken başla.");
            }
        }

        var latestEnd = Time(answers.LatestEnd, "En geç kaça kadar çalışabileceğini seç.", "En geç çalışma saatin");
        if (latestEnd < 1080) throw Invalid("En geç çalışma saatin 18:00 ile 24:00 arasında olmalı.");
        if (latestEnd - studyStart < 60) throw Invalid("En geç çalışma saatin, çalışmaya başladığın saatten en az 1 saat sonra olmalı.");

        var dinnerStart = Time(answers.DinnerStart, "Akşam yemeğinin kaçta başladığını seç.", "Akşam yemeği saatin");
        var dinnerEnd = Time(answers.DinnerEnd, "Akşam yemeğinin kaçta bittiğini seç.", "Akşam yemeği saatin");
        if (dinnerStart < 1020 || dinnerEnd > 1380) throw Invalid("Akşam yemeğin 17:00 ile 23:00 arasında olmalı.");
        if (dinnerEnd - dinnerStart < 30 || dinnerEnd - dinnerStart > 120) throw Invalid("Akşam yemeğin en az yarım saat, en çok 2 saat sürmeli.");

        var busyDays = (answers.BusyDays ?? []).Distinct().Order().ToArray();
        if (busyDays.Any(day => !Weekdays.Contains(day))) throw Invalid("Kurs, etüt ya da antrenman günlerini pazartesi ile cuma arasından seç.");
        int? busyStart = null;
        if (busyDays.Length > 0 && answers.BusyStart is not null)
            busyStart = Time(answers.BusyStart, "Kurs olan günlerde kaçtan sonra çalışabileceğini seç.", "Kurs günlerindeki başlama saatin");

        var weekdayMinutes = Minutes(answers.WeekdayMinutes, "Hafta içi günde toplam ne kadar çalışacağını seç.", "Hafta içi günlük süren");
        if (answers.Weekend is not { } weekend || !Enum.IsDefined(weekend)) throw Invalid("Hafta sonu hangi günler çalışacağını seç.");
        int? weekendStart = null, weekendEnd = null, weekendMinutes = null;
        if (weekend != StudyWeekendDays.None)
        {
            weekendStart = Time(answers.WeekendStart, "Hafta sonu kaçta çalışmaya başlayacağını seç.", "Hafta sonu başlama saatin");
            weekendEnd = Time(answers.WeekendEnd, "Hafta sonu en geç kaça kadar çalışabileceğini seç.", "Hafta sonu bitiş saatin");
            if (weekendEnd - weekendStart < 60) throw Invalid("Hafta sonu bitiş saatin, başlama saatinden en az 1 saat sonra olmalı.");
            weekendMinutes = Minutes(answers.WeekendMinutes, "Hafta sonu günde toplam ne kadar çalışacağını seç.", "Hafta sonu günlük süren");
        }

        if (answers.ExamFocus is { } focus)
        {
            if (!Enum.IsDefined(focus)) throw Invalid("TYT ve AYT ağırlığını seç.");
            if (!preparesForYks) throw Invalid("TYT ve AYT ağırlığını yalnız YKS'ye hazırlanıyorsan seçebilirsin.");
        }

        return new StudyRoutineAnswers(shift, schoolStart, schoolEnd, afterSchool, latestEnd, morningStudy, morningStart, dayStart,
            dinnerStart, dinnerEnd, busyDays, busyStart, weekdayMinutes, weekend, weekendStart, weekendEnd, weekendMinutes, answers.ExamFocus);
    }

    /// <summary>
    /// Turns (validated) answers into sorted, non-overlapping windows of at least <see cref="MinimumWindowMinutes"/> minutes and
    /// the daily budgets. Missing values for the chosen school shift give no window rather than an error.
    /// </summary>
    public static StudyRoutineWindows ToWindows(StudyRoutineAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        var windows = new List<StudyAvailabilityWindow>();
        var budgets = new List<StudyDayBudget>();
        var busy = (answers.BusyDays ?? []).ToHashSet();
        var dinner = answers.DinnerStart is { } ds && answers.DinnerEnd is { } de ? (ds, de) : ((int, int)?)null;
        foreach (var day in Weekdays)
        {
            if (answers.WeekdayMinutes is { } minutes) budgets.Add(new(day, minutes));
            if (busy.Contains(day) && answers.BusyStart is null) continue;   // "o günler çalışamam"
            foreach (var (start, end, morning) in WeekdayPieces(answers))
            {
                var from = start;
                if (busy.Contains(day) && !morning && end > Noon) from = Math.Max(from, answers.BusyStart!.Value);
                foreach (var piece in Subtract((from, end), dinner)) Add(windows, day, piece);
            }
        }
        if (answers.Weekend is { } weekend && weekend != StudyWeekendDays.None && answers.WeekendStart is { } ws && answers.WeekendEnd is { } we)
        {
            DayOfWeek[] days = weekend switch
            {
                StudyWeekendDays.Saturday => [DayOfWeek.Saturday],
                StudyWeekendDays.Sunday => [DayOfWeek.Sunday],
                _ => [DayOfWeek.Saturday, DayOfWeek.Sunday]
            };
            foreach (var day in days)
            {
                if (answers.WeekendMinutes is { } minutes) budgets.Add(new(day, minutes));
                foreach (var lunchFree in Subtract((ws, we), (LunchStart, LunchEnd)))
                    foreach (var piece in Subtract(lunchFree, dinner)) Add(windows, day, piece);
            }
        }
        return new(windows.OrderBy(x => x.Day).ThenBy(x => x.StartMinute).ToArray(), budgets.OrderBy(x => x.Day).ToArray());
    }

    private static IEnumerable<(int Start, int End, bool Morning)> WeekdayPieces(StudyRoutineAnswers a)
    {
        if (a.LatestEnd is not { } latest) yield break;
        switch (a.SchoolShift)
        {
            case StudySchoolShift.Morning or StudySchoolShift.FullDay when a.AfterSchoolStart is { } after:
                yield return (after, latest, false);
                break;
            case StudySchoolShift.Afternoon when a.SchoolStart is { } start && a.SchoolEnd is { } end && a.AfterSchoolStart is { } after:
                if (a.MorningStudy && a.MorningStart is { } morning) yield return (morning, start - 90, true);
                yield return (Math.Max(after, end + 60), latest, false);
                break;
            case StudySchoolShift.NoSchool when a.DayStart is { } dayStart:
                yield return (dayStart, NoSchoolMorningEnd, false);
                yield return (LunchEnd, latest, false);
                break;
        }
    }

    private static IEnumerable<(int Start, int End)> Subtract((int Start, int End) window, (int Start, int End)? cut)
    {
        if (window.End <= window.Start) yield break;
        if (cut is not { } c || c.End <= window.Start || c.Start >= window.End) { yield return window; yield break; }
        if (c.Start > window.Start) yield return (window.Start, c.Start);
        if (c.End < window.End) yield return (c.End, window.End);
    }

    private static void Add(List<StudyAvailabilityWindow> windows, DayOfWeek day, (int Start, int End) piece)
    {
        var start = Math.Max(0, piece.Start); var end = Math.Min(1440, piece.End);
        if (end - start >= MinimumWindowMinutes) windows.Add(new(day, start, end));
    }

    private static int Time(int? value, string missing, string field)
    {
        if (value is not { } minute) throw Invalid(missing);
        if (minute < 0 || minute > 1440 || minute % 30 != 0) throw Invalid($"{field} tam saat ya da buçuk olmalı (00:00–24:00).");
        return minute;
    }

    private static int Minutes(int? value, string missing, string field)
    {
        if (value is not { } minutes) throw Invalid(missing);
        if (minutes < 60 || minutes > 600 || minutes % 30 != 0) throw Invalid($"{field} 1 ile 10 saat arasında, yarım saatlik adımlarla olmalı.");
        return minutes;
    }

    private static StudyPlanValidationException Invalid(string message) => new(message);
}
