using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Parents;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Profiles;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

/// <summary>
/// V2 (parent panel, decision 01.11.2026): the parent overview and the weekly e-mail section read the same measured sources as the
/// student and teacher reports — saved results, older daily records without a result, readings counted once; learning/review
/// and other unmeasured work is activity, never a measurement; assessments stay out; weeks are Istanbul Monday weeks.
/// </summary>
public sealed class ParentReportsPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTime Now = Utc("2026-10-28T10:00:00Z"); // Wednesday, Istanbul 13:00

    private OwnedSpeedReadingDbContext Open() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseNpgsql(postgres.ConnectionString, b => b.MigrationsHistoryTable("__ef_migrations_history", "speed_reading")).Options);

    [Fact]
    public async Task Overview_uses_measured_results_istanbul_weeks_program_and_gamification()
    {
        await using var db = Open(); await Reset(db);
        var seed = await Seed(db);

        var overview = await Reports(db).GetOverviewAsync(seed.Student, Now);

        Assert.Equal(seed.Student, overview.StudentId);
        Assert.Equal(Utc("2026-10-28T08:00:00Z"), overview.LastActivityAt);
        Assert.Equal(DateTimeKind.Utc, overview.LastActivityAt!.Value.Kind);

        var program = Assert.IsType<SpeedReadingParentProgram>(overview.Program);
        Assert.Equal(new SpeedReadingParentProgram("Hızlı Okuma 2", 3, 9, 14, 8, 3, 28, "active"), program);

        Assert.Equal(new SpeedReadingParentThisWeek(18, 2, 4), overview.ThisWeek);
        Assert.Equal(8, overview.Weeks.Count);
        Assert.Equal("2026-09-07", overview.Weeks[0].WeekStart);
        Assert.Equal("2026-10-26", overview.Weeks[^1].WeekStart);
        Assert.Equal(new SpeedReadingParentWeek("2026-10-26", 18, 2), overview.Weeks[^1]);
        Assert.Equal(new SpeedReadingParentWeek("2026-10-19", 10, 2), overview.Weeks[^2]);
        Assert.Equal(new SpeedReadingParentWeek("2026-10-05", 1, 1), overview.Weeks[^4]);
        Assert.All(overview.Weeks.Where(w => w.WeekStart is not ("2026-10-26" or "2026-10-19" or "2026-10-05")),
            week => Assert.Equal((0, 0), (week.Minutes, week.ActiveDays)));

        Assert.Equal(240m, overview.Reading.LatestWpm);
        Assert.Equal(150m, overview.Reading.FirstWpm);
        Assert.Equal(90m, overview.Reading.LatestComprehension);
        Assert.Equal(
            [new SpeedReadingParentTrendPoint("2026-09-01", 150, 60), new SpeedReadingParentTrendPoint("2026-10-20", 200, 90),
             new SpeedReadingParentTrendPoint("2026-10-27", 240, null)],
            overview.Reading.Trend);

        Assert.Equal(
            new[] { new SpeedReadingParentResult("2026-10-28", "Tarama", "Harf avı", 80, null, null),
             new SpeedReadingParentResult("2026-10-27", "Okuma", "Göç eden kuşlar", null, 240, null),
             new SpeedReadingParentResult("2026-10-20", "Anlama", "Metin soruları", 85, 200, 90),
             new SpeedReadingParentResult("2026-10-21", "Tarama", "Harf avı", 70, null, null),
             new SpeedReadingParentResult("2026-09-01", "Okuma", "Göç eden kuşlar", null, 150, 60) }
                .OrderByDescending(r => r.Date).ToList(),
            overview.RecentResults);

        var gamification = Assert.IsType<SpeedReadingParentGamification>(overview.Gamification);
        Assert.Equal((3, 250L, 1, 1), (gamification.Level, gamification.Xp, gamification.StreakDays, gamification.BadgesEarned));
        Assert.Equal([new SpeedReadingParentBadge("İlk Adım", Utc("2026-10-27T09:00:00Z"))], gamification.RecentBadges);
    }

    [Fact]
    public async Task Overview_without_any_data_returns_nulls_and_empty_lists()
    {
        await using var db = Open(); await Reset(db);
        await Seed(db);
        var empty = Guid.NewGuid();

        var overview = await Reports(db).GetOverviewAsync(empty, Now);

        Assert.Null(overview.LastActivityAt);
        Assert.Null(overview.Program);
        Assert.Null(overview.Gamification);
        Assert.Equal(new SpeedReadingParentThisWeek(0, 0, 0), overview.ThisWeek);
        Assert.Equal(8, overview.Weeks.Count);
        Assert.All(overview.Weeks, week => Assert.Equal((0, 0), (week.Minutes, week.ActiveDays)));
        Assert.Equal((null, null, null), (overview.Reading.LatestWpm, overview.Reading.FirstWpm, overview.Reading.LatestComprehension));
        Assert.Empty(overview.Reading.Trend);
        Assert.Empty(overview.RecentResults);
    }

    [Fact]
    public async Task Streak_that_ended_before_yesterday_shows_zero()
    {
        await using var db = Open(); await Reset(db);
        var seed = await Seed(db);
        var overview = await Reports(db).GetOverviewAsync(seed.Student, Now.AddDays(3));
        Assert.Equal(0, overview.Gamification!.StreakDays);
    }

    [Fact]
    public async Task Weekly_summary_covers_one_istanbul_week()
    {
        await using var db = Open(); await Reset(db);
        var seed = await Seed(db);
        var reports = Reports(db);

        Assert.Equal(new SpeedReadingParentWeeklySummary(18, 2, 4, 240, 40, null, 9, 14, 1),
            await reports.GetWeeklySummaryAsync(seed.Student, new DateOnly(2026, 10, 26), Now));
        Assert.Equal(new SpeedReadingParentWeeklySummary(10, 2, 2, 200, 50, 90, 9, 14, 0),
            await reports.GetWeeklySummaryAsync(seed.Student, new DateOnly(2026, 10, 19), Now));
        Assert.Equal(new SpeedReadingParentWeeklySummary(0, 0, 0, null, null, null, 9, 14, 0),
            await reports.GetWeeklySummaryAsync(seed.Student, new DateOnly(2026, 9, 14), Now));
    }

    [Fact]
    public async Task Weekly_summary_is_null_only_without_any_speed_reading_footprint()
    {
        await using var db = Open(); await Reset(db);
        await Seed(db);
        var profileOnly = Guid.NewGuid();
        db.UserProfiles.Add(SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), profileOnly, Now));
        await db.SaveChangesAsync();
        var reports = Reports(db);

        Assert.Null(await reports.GetWeeklySummaryAsync(Guid.NewGuid(), new DateOnly(2026, 10, 26), Now));
        Assert.Equal(new SpeedReadingParentWeeklySummary(0, 0, 0, null, null, null, null, null, 0),
            await reports.GetWeeklySummaryAsync(profileOnly, new DateOnly(2026, 10, 26), Now));
    }

    private sealed record Seeded(Guid Student);

    private static async Task<Seeded> Seed(OwnedSpeedReadingDbContext db)
    {
        var student = Guid.NewGuid(); var other = Guid.NewGuid(); var actor = Guid.NewGuid();
        var text = ReadingText.Create(Guid.NewGuid(), "Göç eden kuşlar", "Metin", difficultyLevel: 1);
        db.ReadingTexts.Add(text);
        var scanning = ExerciseType.Create(Guid.NewGuid(), "Scanning", "Tarama", "scanning");
        var vocabulary = ExerciseType.Create(Guid.NewGuid(), "Vocabulary", "Kelime", "vocabulary_builder");
        var comprehension = ExerciseType.Create(Guid.NewGuid(), "Comprehension", "Anlama", "reading_comprehension");
        db.ExerciseTypes.AddRange(scanning, vocabulary, comprehension);
        var hunt = Exercise.Create("Harf avı", "scanning", "{}", 1, actor, scanning.Id);
        var words = Exercise.Create("Kelime kartları", "vocabulary_builder", "{}", 1, actor, vocabulary.Id);
        var questions = Exercise.Create("Metin soruları", "reading_comprehension", "{}", 1, actor, comprehension.Id);
        db.Exercises.AddRange(hunt, words, questions);

        // Readings: an old measured baseline, a measured reading without questions, an unmeasured one, a rejected one.
        db.ReadingSessions.Add(Reading(student, text, Utc("2026-09-01T09:00:00Z"), 60, 150, true, 3, 60));
        var flow = Session(db, student, questions, Utc("2026-10-27T08:00:00Z"));
        db.ReadingSessions.Add(ReadingSession.Import(flow, student, text.Id, 300, 240, 0, 0, 0, 0,
            Utc("2026-10-27T08:00:00Z"), Utc("2026-10-27T08:00:00Z"), null, null, null, isMeasured: true));
        // The reading flow's own result is the same activity and must be counted once (as the reading).
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), flow, student, questions.Id, text.Id, 1200, 300,
            240, 0, 0, 90, Utc("2026-10-27T08:00:00Z")));
        db.ReadingSessions.Add(Reading(student, text, Utc("2026-10-26T22:30:00Z"), 120, 999, false, 0, 0));
        db.ReadingSessions.Add(Reading(student, text, Utc("2026-10-05T09:00:00Z"), 60, 400, false, 3, 100, rejected: true));

        // Exercises: measured scanning, an unmeasured vocabulary learning round, a measured comprehension text, an assessment.
        var huntSession = Session(db, student, hunt, Utc("2026-10-28T07:00:00Z"));
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), huntSession, student, hunt.Id, null, 0, 600,
            0, 0, 0, 80, Utc("2026-10-28T07:00:00Z")));
        var learnSession = Session(db, student, words, Utc("2026-10-28T08:00:00Z"));
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), learnSession, student, words.Id, null, 0, 60,
            0, 0, 0, 0, Utc("2026-10-28T08:00:00Z"), isMeasured: false));
        var textSession = Session(db, student, questions, Utc("2026-10-20T09:00:00Z"));
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), textSession, student, questions.Id, text.Id, 600, 300,
            200, 90, 0, 85, Utc("2026-10-20T09:00:00Z"), questionAnswersJson: """[{"questionId":"q1","answer":"a"}]"""));
        var assessmentSession = Session(db, student, questions, Utc("2026-10-28T09:00:00Z"));
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), assessmentSession, student, questions.Id, text.Id, 600, 900,
            500, 100, 0, 100, Utc("2026-10-28T09:00:00Z"), questionAnswersJson: """[{"questionId":"q1","answer":"a"}]""", isAssessmentMode: true));
        // Another student's activity never leaks in.
        var otherSession = Session(db, other, hunt, Utc("2026-10-28T07:00:00Z"));
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), otherSession, other, hunt.Id, null, 0, 6000,
            0, 0, 0, 10, Utc("2026-10-28T07:00:00Z")));
        db.ReadingSessions.Add(Reading(other, text, Utc("2026-10-28T07:00:00Z"), 600, 999, true, 3, 10));

        // Program: the active one (14 days, 2 tasks a day) and an older finished one.
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Hızlı Okuma 2", "", Guid.NewGuid(), 0, 100, "{}", 2, 2, 5, 2, 14,
            true, 1, 0, null, false, Now, null, null, null);
        var oldTemplate = ProgramTemplate.Import(Guid.NewGuid(), "Hızlı Okuma 1", "", Guid.NewGuid(), 0, 100, "{}", 1, 2, 5, 1, 7,
            true, 1, 0, null, false, Now, null, null, null);
        db.ProgramTemplates.AddRange(template, oldTemplate);
        var progress = StudentProgramProgress.Import(Guid.NewGuid(), student, template.Id, Utc("2026-10-20T06:00:00Z"), 3, 2, 3, 8, 20,
            Utc("2026-10-27T08:00:00Z"), true, null, 80, 2, 4, Utc("2026-10-20T06:00:00Z"), null, null, null);
        progress.SetSchedule(JsonSerializer.Serialize(Enumerable.Range(1, 14).SelectMany(day => new[] { 1, 2 }.Select(order => new
        {
            WeekNumber = (day - 1) / 7 + 1, DayNumber = (day - 1) % 7 + 1, Order = order, ExerciseId = hunt.Id
        }))), actor, Now);
        var oldProgress = StudentProgramProgress.Import(Guid.NewGuid(), student, oldTemplate.Id, Utc("2026-09-01T06:00:00Z"), 7, 1, 1, 7, 14,
            Utc("2026-09-08T08:00:00Z"), false, Utc("2026-09-08T08:00:00Z"), 75, 0, 4, Utc("2026-09-01T06:00:00Z"), null, null, null);
        db.StudentProgramProgresses.AddRange(progress, oldProgress);
        // An older daily record without a saved result is still a measured exercise; records with a result are not counted twice.
        db.DailyExerciseLogs.Add(Log(student, progress.Id, hunt, Utc("2026-10-21T09:00:00Z"), null, 1, 1, 300, 70, true));
        db.DailyExerciseLogs.Add(Log(student, progress.Id, hunt, Utc("2026-10-28T07:00:00Z"), huntSession, 2, 1, 600, 80, true));
        db.DailyExerciseLogs.Add(Log(student, progress.Id, words, Utc("2026-10-28T08:00:00Z"), learnSession, 2, 2, 60, 0, false));
        db.DailyExerciseLogs.Add(Log(student, oldProgress.Id, hunt, Utc("2026-09-02T09:00:00Z"), assessmentSession, 1, 1, 60, 0, false));

        // Gamification: 250 XP (level 3), a streak with its last day yesterday, one live badge and one deleted badge.
        var stats = UserGamification.CreateDefault(Guid.NewGuid(), student, Now, null);
        stats.AwardXp(250, actor, Now);
        stats.UpdateStreak(Utc("2026-10-27T08:00:00Z"), 5, actor, Now);
        db.UserGamifications.Add(stats);
        var first = Achievement.Create(Guid.NewGuid(), "İlk Adım", "İlk egzersizini tamamla.", "Exercise", "Bronze", null, "🎯",
            "exercise_count", "{\"count\":1}", null, null, false, 10, true, 1, actor, Now);
        var removed = Achievement.Create(Guid.NewGuid(), "Eski rozet", "Kaldırıldı.", "Exercise", "Bronze", null, "🎯",
            "exercise_count", "{\"count\":2}", null, null, false, 10, true, 2, actor, Now);
        removed.Delete(actor, Now);
        db.Achievements.AddRange(first, removed);
        db.UserAchievements.Add(UserAchievement.Import(Guid.NewGuid(), student, first.Id, Utc("2026-10-27T09:00:00Z"), false, null,
            false, null, null, Now, null, null, null));
        db.UserAchievements.Add(UserAchievement.Import(Guid.NewGuid(), student, removed.Id, Utc("2026-10-27T10:00:00Z"), false, null,
            false, null, null, Now, null, null, null));
        await db.SaveChangesAsync();
        return new Seeded(student);
    }

    private static Guid Session(OwnedSpeedReadingDbContext db, Guid user, Exercise exercise, DateTime at)
    {
        var session = ExerciseSession.Start(user, exercise.Id, null, 1, at.AddMinutes(-5), null);
        session.Complete(at);
        db.ExerciseSessions.Add(session);
        return session.Id;
    }

    private static ReadingSession Reading(Guid user, ReadingText text, DateTime at, int seconds, int wpm, bool measured,
        int questions, decimal comprehension, bool rejected = false) =>
        ReadingSession.Import(Guid.NewGuid(), user, text.Id, seconds, wpm, questions == 0 ? 0 : 1, questions, comprehension, 0, at, at,
            null, null, null, isMeasured: measured, isRejected: rejected);

    private static DailyExerciseLog Log(Guid user, Guid progress, Exercise exercise, DateTime at, Guid? session, int day, int slot,
        int seconds, decimal score, bool measured) =>
        DailyExerciseLog.Import(Guid.NewGuid(), user, progress, exercise.Id, exercise.ExerciseTypeId, day, 1, 1, at,
            seconds, score, measured, "{}", 1, false, "verification", 1, 0, 1, null, null, 4, TimeSpan.Zero,
            0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 0, at, null, null, null, isMeasured: measured, sessionId: session, slotOrder: slot);

    private static ISpeedReadingParentReports Reports(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingParentReports)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingParentReports")!, db)!;

    private static DateTime Utc(string value) =>
        DateTime.Parse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

    private static async Task Reset(OwnedSpeedReadingDbContext db)
    {
        await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
    }
}
