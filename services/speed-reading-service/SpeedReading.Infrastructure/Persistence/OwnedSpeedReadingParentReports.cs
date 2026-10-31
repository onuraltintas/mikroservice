using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.Parents;
using SpeedReading.Domain.Gamification;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// V2 (parent panel, decision 01.11.2026): what a parent sees of a child's speed reading. Every number comes from the sources the
/// student and teacher reports use — readings plus <see cref="OwnedSpeedReadingReportExercises.WithoutReadingDuplicates"/> — and
/// the same measured rules (H21/H22): speed only from measured readings/results with a speed, comprehension only where questions
/// about a text were answered (stored on the first answer), unmeasured work (learning/review rounds, warm-ups) counts as activity
/// but never as a result, assessments stay in the placement report. Days and weeks are Europe/Istanbul, weeks start on Monday.
/// Each query is bounded (a date range or a small Take), so a long history never loads into memory.
/// </summary>
internal sealed class OwnedSpeedReadingParentReports(OwnedSpeedReadingDbContext db) : ISpeedReadingParentReports
{
    private const string ReadingType = "Okuma";
    private const string ReadingFallbackTitle = "Okuma metni";

    public async Task<SpeedReadingParentOverview> GetOverviewAsync(Guid studentUserId, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var currentWeek = SpeedReadingParentCalendar.CurrentWeekStart(nowUtc);
        var firstWeek = currentWeek.AddDays(-7 * (SpeedReadingParentCalendar.OverviewWeeks - 1));
        var days = await DailyActivityAsync(studentUserId, firstWeek, currentWeek.AddDays(7), cancellationToken);
        var weeks = Enumerable.Range(0, SpeedReadingParentCalendar.OverviewWeeks)
            .Select(index => firstWeek.AddDays(7 * index))
            .Select(start => Week(days, start))
            .ToList();
        var thisWeek = Week(days, currentWeek);

        var trend = await TrendAsync(studentUserId, cancellationToken);
        var reading = new SpeedReadingParentReading(
            await LatestWpmAsync(studentUserId, null, null, descending: true, cancellationToken),
            await LatestWpmAsync(studentUserId, null, null, descending: false, cancellationToken),
            await LatestComprehensionAsync(studentUserId, cancellationToken),
            trend);

        return new SpeedReadingParentOverview(
            studentUserId,
            await LastActivityAsync(studentUserId, cancellationToken),
            await ProgramAsync(studentUserId, nowUtc, cancellationToken),
            new SpeedReadingParentThisWeek(thisWeek.Minutes, thisWeek.ActiveDays, thisWeek.Activities),
            weeks.Select(week => new SpeedReadingParentWeek(SpeedReadingParentCalendar.Format(week.Start), week.Minutes, week.ActiveDays)).ToList(),
            reading,
            await GamificationAsync(studentUserId, nowUtc, cancellationToken),
            await RecentResultsAsync(studentUserId, cancellationToken));
    }

    public async Task<SpeedReadingParentWeeklySummary?> GetWeeklySummaryAsync(Guid studentUserId, DateOnly weekStart, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = SpeedReadingParentCalendar.WeekRangeUtc(weekStart);
        var week = Week(await DailyActivityAsync(studentUserId, weekStart, weekStart.AddDays(7), cancellationToken), weekStart);
        var program = await ProgramAsync(studentUserId, nowUtc, cancellationToken);
        if (week.Activities == 0 && program is null && !await HasAnyFootprintAsync(studentUserId, cancellationToken))
            return null;

        var latestWpm = await LatestWpmAsync(studentUserId, start, end, descending: true, cancellationToken);
        var previousWpm = latestWpm is null ? null : await LatestWpmAsync(studentUserId, null, start, descending: true, cancellationToken);
        var comprehension = await db.ReadingSessions.AsNoTracking()
            .Where(item => item.UserId == studentUserId && item.CompletedAt >= start && item.CompletedAt < end
                && item.TotalQuestions > 0 && !item.IsRejected)
            .Select(item => (decimal?)item.ComprehensionRate)
            .ToListAsync(cancellationToken);
        comprehension.AddRange(await Exercises(studentUserId)
            .Where(item => item.CompletedDate >= start && item.CompletedDate < end && item.AverageComprehension != null)
            .Select(item => item.AverageComprehension)
            .ToListAsync(cancellationToken));
        var badges = await Badges(studentUserId)
            .CountAsync(item => item.UnlockedAt >= start && item.UnlockedAt < end, cancellationToken);

        return new SpeedReadingParentWeeklySummary(
            week.Minutes,
            week.ActiveDays,
            week.Activities,
            latestWpm,
            latestWpm is { } latest && previousWpm is { } previous ? latest - previous : null,
            comprehension.Count == 0 ? null : RoundComprehension(comprehension.Average()),
            program?.CurrentDay,
            program?.TotalDays,
            badges);
    }

    // Activity per Istanbul day: readings and exercises (measured or not), each counted once.
    private async Task<IReadOnlyDictionary<DateOnly, DayActivity>> DailyActivityAsync(Guid userId, DateOnly fromDay, DateOnly toDayExclusive,
        CancellationToken cancellationToken)
    {
        var start = SpeedReadingReportCalendar.LocalDayStartUtc(fromDay);
        var end = SpeedReadingReportCalendar.LocalDayStartUtc(toDayExclusive);
        var readings = await db.ReadingSessions.AsNoTracking()
            .Where(item => item.UserId == userId && item.CompletedAt >= start && item.CompletedAt < end)
            .GroupBy(item => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.CompletedAt, SpeedReadingReportCalendar.TimeZoneId).Date)
            .Select(group => new { Date = group.Key, Count = group.Count(), Seconds = group.Sum(item => (long)item.ReadingTimeSeconds) })
            .ToListAsync(cancellationToken);
        var exercises = await Exercises(userId)
            .Where(item => item.CompletedDate >= start && item.CompletedDate < end)
            .GroupBy(item => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(item.CompletedDate, SpeedReadingReportCalendar.TimeZoneId).Date)
            .Select(group => new { Date = group.Key, Count = group.Count(), Seconds = group.Sum(item => (long)item.TimeSpentSeconds) })
            .ToListAsync(cancellationToken);
        return readings.Concat(exercises)
            .GroupBy(item => DateOnly.FromDateTime(item.Date))
            .ToDictionary(group => group.Key, group => new DayActivity(group.Sum(item => item.Count), group.Sum(item => item.Seconds)));
    }

    private static WeekActivity Week(IReadOnlyDictionary<DateOnly, DayActivity> days, DateOnly start)
    {
        var inWeek = Enumerable.Range(0, 7).Select(offset => days.GetValueOrDefault(start.AddDays(offset))).Where(day => day is not null).ToList();
        return new WeekActivity(start,
            SpeedReadingParentCalendar.Minutes(inWeek.Sum(day => day!.Seconds)),
            inWeek.Count(day => day!.Count > 0),
            inWeek.Sum(day => day!.Count));
    }

    private async Task<DateTime?> LastActivityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var reading = await db.ReadingSessions.AsNoTracking().Where(item => item.UserId == userId)
            .MaxAsync(item => (DateTime?)item.CompletedAt, cancellationToken);
        var exercise = await Exercises(userId).MaxAsync(item => (DateTime?)item.CompletedDate, cancellationToken);
        var latest = new[] { reading, exercise }.Where(item => item.HasValue).Select(item => item!.Value).DefaultIfEmpty().Max();
        return latest == default ? null : Utc(latest);
    }

    private async Task<IReadOnlyList<SpeedReadingParentTrendPoint>> TrendAsync(Guid userId, CancellationToken cancellationToken)
    {
        var readings = await MeasuredReadings(userId)
            .OrderByDescending(item => item.CompletedAt)
            .Take(SpeedReadingParentCalendar.TrendPoints)
            .Select(item => new MeasuredPoint(item.CompletedAt,
                item.IsMeasured && item.CalculatedWpm > 0 ? (decimal?)item.CalculatedWpm : null,
                item.TotalQuestions > 0 && !item.IsRejected ? (decimal?)item.ComprehensionRate : null))
            .ToListAsync(cancellationToken);
        var exercises = await Exercises(userId)
            .Where(item => item.AverageWPM != null || item.AverageComprehension != null)
            .OrderByDescending(item => item.CompletedDate)
            .Take(SpeedReadingParentCalendar.TrendPoints)
            .Select(item => new MeasuredPoint(item.CompletedDate, item.AverageWPM, item.AverageComprehension))
            .ToListAsync(cancellationToken);
        return readings.Concat(exercises)
            .OrderByDescending(item => item.At)
            .Take(SpeedReadingParentCalendar.TrendPoints)
            .OrderBy(item => item.At)
            .Select(item => new SpeedReadingParentTrendPoint(LocalDay(item.At), RoundWpm(item.Wpm), RoundComprehension(item.Comprehension)))
            .ToList();
    }

    private async Task<IReadOnlyList<SpeedReadingParentResult>> RecentResultsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var readings = await MeasuredReadings(userId)
            .OrderByDescending(item => item.CompletedAt)
            .Take(SpeedReadingParentCalendar.RecentResults)
            .Select(item => new
            {
                item.CompletedAt,
                Title = db.ReadingTexts.Where(text => text.Id == item.ReadingTextId).Select(text => text.Title).FirstOrDefault(),
                Wpm = item.IsMeasured && item.CalculatedWpm > 0 ? (decimal?)item.CalculatedWpm : null,
                Comprehension = item.TotalQuestions > 0 && !item.IsRejected ? (decimal?)item.ComprehensionRate : null
            })
            .ToListAsync(cancellationToken);
        var exercises = await Exercises(userId)
            .Where(item => item.IsMeasured)
            .OrderByDescending(item => item.CompletedDate)
            .Take(SpeedReadingParentCalendar.RecentResults)
            .Select(item => new { item.CompletedDate, item.TypeName, item.Title, item.SuccessRate, item.AverageWPM, item.AverageComprehension })
            .ToListAsync(cancellationToken);
        return readings
            .Select(item => (At: item.CompletedAt, Result: new SpeedReadingParentResult(LocalDay(item.CompletedAt), ReadingType,
                string.IsNullOrWhiteSpace(item.Title) ? ReadingFallbackTitle : item.Title, null, RoundWpm(item.Wpm),
                RoundComprehension(item.Comprehension))))
            .Concat(exercises.Select(item => (At: item.CompletedDate, Result: new SpeedReadingParentResult(LocalDay(item.CompletedDate),
                item.TypeName, item.Title, Math.Round(item.SuccessRate, 1), RoundWpm(item.AverageWPM),
                RoundComprehension(item.AverageComprehension)))))
            .OrderByDescending(item => item.At)
            .Take(SpeedReadingParentCalendar.RecentResults)
            .Select(item => item.Result)
            .ToList();
    }

    /// <summary>The most recent (or first) measured speed, optionally inside [from, to).</summary>
    private async Task<decimal?> LatestWpmAsync(Guid userId, DateTime? from, DateTime? to, bool descending, CancellationToken cancellationToken)
    {
        var readings = db.ReadingSessions.AsNoTracking()
            .Where(item => item.UserId == userId && item.IsMeasured && item.CalculatedWpm > 0);
        var exercises = Exercises(userId).Where(item => item.AverageWPM != null);
        if (from is { } start)
        {
            readings = readings.Where(item => item.CompletedAt >= start);
            exercises = exercises.Where(item => item.CompletedDate >= start);
        }
        if (to is { } end)
        {
            readings = readings.Where(item => item.CompletedAt < end);
            exercises = exercises.Where(item => item.CompletedDate < end);
        }
        var reading = await (descending ? readings.OrderByDescending(item => item.CompletedAt) : readings.OrderBy(item => item.CompletedAt))
            .Select(item => new MeasuredPoint(item.CompletedAt, (decimal?)item.CalculatedWpm, null))
            .FirstOrDefaultAsync(cancellationToken);
        var exercise = await (descending ? exercises.OrderByDescending(item => item.CompletedDate) : exercises.OrderBy(item => item.CompletedDate))
            .Select(item => new MeasuredPoint(item.CompletedDate, item.AverageWPM, null))
            .FirstOrDefaultAsync(cancellationToken);
        var pick = new[] { reading, exercise }.Where(item => item is not null)
            .OrderBy(item => descending ? -item!.At.Ticks : item!.At.Ticks)
            .FirstOrDefault();
        return RoundWpm(pick?.Wpm);
    }

    private async Task<decimal?> LatestComprehensionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var reading = await db.ReadingSessions.AsNoTracking()
            .Where(item => item.UserId == userId && item.TotalQuestions > 0 && !item.IsRejected)
            .OrderByDescending(item => item.CompletedAt)
            .Select(item => new MeasuredPoint(item.CompletedAt, null, (decimal?)item.ComprehensionRate))
            .FirstOrDefaultAsync(cancellationToken);
        var exercise = await Exercises(userId)
            .Where(item => item.AverageComprehension != null)
            .OrderByDescending(item => item.CompletedDate)
            .Select(item => new MeasuredPoint(item.CompletedDate, null, item.AverageComprehension))
            .FirstOrDefaultAsync(cancellationToken);
        var pick = new[] { reading, exercise }.Where(item => item is not null).OrderByDescending(item => item!.At).FirstOrDefault();
        return RoundComprehension(pick?.Comprehension);
    }

    private async Task<SpeedReadingParentProgram?> ProgramAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var row = await (
                from progress in db.StudentProgramProgresses.AsNoTracking()
                join template in db.ProgramTemplates.AsNoTracking() on progress.ProgramTemplateId equals template.Id
                where progress.UserId == userId && !progress.IsStaffTraining
                orderby progress.IsActive descending, progress.AssignedDate descending
                select new { Progress = progress, template.Name, template.TotalDays })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
            return null;

        var progressId = row.Progress.Id;
        var completedTasks = await db.DailyExerciseLogs.AsNoTracking()
            .Where(item => item.StudentProgramProgressId == progressId)
            .Select(item => new { item.WeekNumber, item.DayNumber, item.SlotOrder, item.ExerciseId })
            .Distinct()
            .CountAsync(cancellationToken);
        var schedule = OwnedSpeedReadingProgramSchedule.Parse(row.Progress.ScheduleJson);
        // The program length the student sees: the scheduled days, or the template length before a schedule exists.
        var totalDays = schedule.Count > 0 ? schedule.Max(slot => (slot.WeekNumber - 1) * 7 + slot.DayNumber) : row.TotalDays;
        var completed = row.Progress.CompletedDate.HasValue;
        var stored = Math.Max(1, (row.Progress.CurrentWeek - 1) * 7 + row.Progress.CurrentDay);
        var visible = completed
            ? totalDays
            : Math.Min(stored, Math.Max(1, SpeedReadingDailyProgressRules.GetAvailableDay(
                row.Progress.AssignedDate, nowUtc, row.Progress.IsStaffTraining, row.Progress.EarlyAdvanceDays)));
        if (totalDays > 0)
            visible = Math.Min(visible, totalDays);
        return new SpeedReadingParentProgram(
            row.Name,
            row.Progress.CurrentDifficultyLevel,
            visible,
            totalDays,
            row.Progress.DaysCompleted,
            schedule.Count > 0 ? Math.Min(completedTasks, schedule.Count) : completedTasks,
            schedule.Count,
            completed ? "completed" : row.Progress.IsActive ? "active" : "inactive");
    }

    private async Task<SpeedReadingParentGamification?> GamificationAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var stats = await db.UserGamifications.AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .Select(item => new { item.TotalXP, item.CurrentStreak, item.LastActivityDate })
            .FirstOrDefaultAsync(cancellationToken);
        var badges = Badges(userId);
        var earned = await badges.CountAsync(cancellationToken);
        if (stats is null && earned == 0)
            return null;
        var recent = await badges
            .OrderByDescending(item => item.UnlockedAt)
            .Take(SpeedReadingParentCalendar.RecentBadges)
            .ToListAsync(cancellationToken);
        var xp = stats?.TotalXP ?? 0;
        return new SpeedReadingParentGamification(
            GamificationRules.CalculateLevel(xp),
            xp,
            stats is null ? 0 : GamificationRules.CurrentStreak(stats.LastActivityDate, stats.CurrentStreak, nowUtc),
            earned,
            recent.Select(item => new SpeedReadingParentBadge(item.Name, Utc(item.UnlockedAt))).ToList());
    }

    private async Task<bool> HasAnyFootprintAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserProfiles.AsNoTracking().AnyAsync(item => item.UserId == userId, cancellationToken)
        || await db.StudentProgramProgresses.AsNoTracking().AnyAsync(item => item.UserId == userId && !item.IsStaffTraining, cancellationToken)
        || await db.ReadingSessions.AsNoTracking().AnyAsync(item => item.UserId == userId, cancellationToken)
        || await Exercises(userId).AnyAsync(cancellationToken)
        || await db.UserGamifications.AsNoTracking().AnyAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken);

    private IQueryable<ReportedExercise> Exercises(Guid userId) =>
        OwnedSpeedReadingReportExercises.WithoutReadingDuplicates(db).Where(item => item.UserId == userId);

    private IQueryable<Domain.Sessions.ReadingSession> MeasuredReadings(Guid userId) =>
        db.ReadingSessions.AsNoTracking().Where(item => item.UserId == userId
            && ((item.IsMeasured && item.CalculatedWpm > 0) || (item.TotalQuestions > 0 && !item.IsRejected)));

    private IQueryable<BadgeRow> Badges(Guid userId) =>
        from earned in db.UserAchievements.AsNoTracking()
        join achievement in db.Achievements.AsNoTracking() on earned.AchievementId equals achievement.Id
        where earned.UserId == userId && !earned.IsDeleted && !achievement.IsDeleted
        select new BadgeRow { Name = achievement.Name, UnlockedAt = earned.UnlockedAt };

    private static string LocalDay(DateTime utc) => SpeedReadingParentCalendar.Format(SpeedReadingReportCalendar.LocalDate(Utc(utc)));

    private static decimal? RoundWpm(decimal? value) => value is { } wpm ? Math.Round(wpm, 0, MidpointRounding.AwayFromZero) : null;

    private static decimal? RoundComprehension(decimal? value) =>
        value is { } rate ? Math.Round(rate, 1, MidpointRounding.AwayFromZero) : null;

    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private sealed record DayActivity(int Count, long Seconds);

    private sealed record WeekActivity(DateOnly Start, int Minutes, int ActiveDays, int Activities);

    private sealed record MeasuredPoint(DateTime At, decimal? Wpm, decimal? Comprehension);

    private sealed class BadgeRow
    {
        public string Name { get; init; } = string.Empty;
        public DateTime UnlockedAt { get; init; }
    }
}
