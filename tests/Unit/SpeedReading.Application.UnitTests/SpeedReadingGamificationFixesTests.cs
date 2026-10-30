using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Gamification;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Profiles;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// H19 (audit 30.10.2026, decisions 30.10.2026): XP only for the first verified completion of an exercise per Istanbul
/// day; speed badges only count with comprehension ≥60; RSVP badges count without a measured WPM; badge rules and the
/// rebuild follow the live rules; earned badges and level-ups reach the student.
/// </summary>
public sealed class SpeedReadingGamificationFixesTests
{
    private const string Target = "b";
    private static readonly Guid Actor = Guid.NewGuid();

    // ---- HIGH 3 / MEDIUM 4: replay XP ----

    [Fact]
    public async Task Only_the_first_completion_of_an_exercise_on_a_day_earns_xp()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await SeedTargetHunt(db, user);
        var service = Service(db);

        var first = await PlayTargetHunt(service, user, id);
        var replay = await PlayTargetHunt(service, user, id);

        Assert.True(first.XpGained > 0);
        Assert.Equal(0, replay.XpGained);
        var stats = await db.UserGamifications.SingleAsync(item => item.UserId == user);
        Assert.Equal(first.XpGained, stats.TotalXP);
        Assert.Equal(2, stats.TotalActivitiesCompleted); // the replay still counts as activity and keeps the streak
        var stored = await db.ExerciseSessionResults.SingleAsync(item => item.SessionId == replay.SessionId);
        Assert.Equal(0, stored.GamificationXp);
        Assert.True(stored.CountsForGamification);

        // Reading the finished replay again reports what was credited, not a recomputed amount.
        var again = await service.CompleteAsync(user, replay.SessionId, new());
        Assert.Equal(0, again.XpGained);
    }

    [Fact]
    public async Task A_completion_on_the_next_istanbul_day_earns_xp_again()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await SeedTargetHunt(db, user);
        var service = Service(db);
        var first = await PlayTargetHunt(service, user, id);
        var stored = await db.ExerciseSessionResults.SingleAsync(item => item.SessionId == first.SessionId);
        typeof(ExerciseSessionResult).GetProperty(nameof(ExerciseSessionResult.CompletedAt))!
            .SetValue(stored, DateTime.UtcNow.AddDays(-1));
        await db.SaveChangesAsync();

        var next = await PlayTargetHunt(service, user, id);

        Assert.True(next.XpGained > 0);
    }

    // ---- MEDIUM 5: new badges and level-ups are reported and notified ----

    [Fact]
    public async Task New_badges_and_level_ups_are_returned_and_notified()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var id = await SeedTargetHunt(db, user);
        db.Achievements.Add(Achievement.Create(Guid.NewGuid(), "İlk Adım", "İlk egzersizini tamamla.", "Exercise", "Bronze",
            null, "🎯", "exercise_count", "{\"count\":1}", null, null, false, 200, true, 1));
        await db.SaveChangesAsync();

        var result = await PlayTargetHunt(Service(db), user, id);

        Assert.Equal(["İlk Adım"], result.UnlockedBadges);
        Assert.True(result.LeveledUp);
        Assert.Equal(GamificationRules.CalculateLevel(result.XpGained + 200), result.NewLevel);
        var notification = Assert.Single(((ISpeedReadingDataContext)db).Notifications.Where(item => item.UserId == user));
        Assert.Equal(11, notification.Type);
        Assert.Contains("İlk Adım", notification.Message);
    }

    // ---- HIGH 1 / MEDIUM 6 / MEDIUM 7: what feeds badge statistics ----

    [Fact]
    public void Rsvp_sessions_count_without_a_measured_wpm_and_the_display_pace_counts_with_good_comprehension()
    {
        var stats = Stats();
        stats.RecordVerifiedExerciseCompletion(new VerifiedExerciseCompletion("RSVP", DateTime.UtcNow, 90, null, 80m, false, 20,
            RsvpPaceWpm: 320), Actor, DateTime.UtcNow);
        stats.RecordVerifiedExerciseCompletion(new VerifiedExerciseCompletion("RSVP", DateTime.UtcNow, 90, null, 40m, false, 0,
            RsvpPaceWpm: 500), Actor, DateTime.UtcNow);

        Assert.Equal(2, stats.TotalRsvpSessionsCompleted);
        Assert.Equal(320, stats.MaxRSVPWPM);
        Assert.Equal(0, stats.TotalReadingSessionsCompleted);
    }

    [Fact]
    public void Speed_only_counts_with_at_least_60_percent_comprehension_on_a_session_with_questions()
    {
        var stats = Stats();
        stats.RecordVerifiedExerciseCompletion(new VerifiedExerciseCompletion("Comprehension", DateTime.UtcNow, 60, 500, 0m, true, 10), Actor, DateTime.UtcNow);
        stats.RecordVerifiedExerciseCompletion(new VerifiedExerciseCompletion("FreeReading", DateTime.UtcNow, 60, 450, null, true, 10), Actor, DateTime.UtcNow);
        Assert.Equal(0, stats.MaxWPM);

        stats.RecordVerifiedExerciseCompletion(new VerifiedExerciseCompletion("Comprehension", DateTime.UtcNow, 60, 260, 60m, true, 10), Actor, DateTime.UtcNow);
        Assert.Equal(260, stats.MaxWPM);
        Assert.Equal(3, stats.TotalReadingSessionsCompleted);
    }

    [Fact]
    public void The_comprehension_badge_only_counts_full_comprehension_texts()
    {
        var stats = Stats();
        stats.RecordVerifiedExerciseCompletion(new VerifiedExerciseCompletion("Visualization", DateTime.UtcNow, 60, null, 100m, false, 10), Actor, DateTime.UtcNow);
        Assert.Equal(0, stats.MaxComprehensionScore);

        stats.RecordVerifiedExerciseCompletion(new VerifiedExerciseCompletion("Comprehension", DateTime.UtcNow, 60, null, 90m, false, 10,
            CountsForComprehensionBadge: true), Actor, DateTime.UtcNow);
        Assert.Equal(90, stats.MaxComprehensionScore);
    }

    // ---- MEDIUM 8: streak freeze ----

    [Fact]
    public void A_streak_freeze_only_bridges_a_single_missed_day()
    {
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var stats = Stats();
        stats.UpdateStreak(now.AddDays(-2), 5, Actor, now.AddDays(-2));
        stats.UseStreakFreeze(Actor, now);
        Assert.Equal(GamificationRules.IstanbulDate(now).AddDays(-1), GamificationRules.IstanbulDate(stats.LastActivityDate!.Value));
        Assert.Equal(2, stats.StreakFreezeCount);

        var stale = Stats();
        stale.UpdateStreak(now.AddDays(-20), 5, Actor, now.AddDays(-20));
        Assert.Throws<InvalidOperationException>(() => stale.UseStreakFreeze(Actor, now));
        var current = Stats();
        current.UpdateStreak(now.AddDays(-1), 5, Actor, now.AddDays(-1));
        Assert.Throws<InvalidOperationException>(() => current.UseStreakFreeze(Actor, now));
        Assert.Equal(3, stale.StreakFreezeCount);
    }

    // ---- MEDIUM 9 / MEDIUM 10: badge rules ----

    [Theory]
    [InlineData("Chunking")]
    [InlineData("EyeTracking")]
    [InlineData("Saccade")]
    [InlineData("SpeedReading")]
    [InlineData("Focus")]
    [InlineData("Bilinmeyen")]
    public void A_first_exercise_badge_needs_a_type_that_can_be_verified(string type)
    {
        Assert.ThrowsAny<ArgumentException>(() => AchievementCriteriaRules.Validate("exercise_type_first", $"{{\"type\":\"{type}\"}}", false));
    }

    [Fact]
    public void Badge_edits_are_validated_like_new_badges()
    {
        AchievementCriteriaRules.Validate("exercise_type_first", "{\"type\":\"Tachistoscope\"}", false);
        var badge = Achievement.Create(Guid.NewGuid(), "Seri", "5 gün", "Streak", "Bronze", null, "🔥", "streak", "{\"days\":5}",
            null, null, false, 10, true, 1);

        Assert.ThrowsAny<ArgumentException>(() => badge.Update("Seri", "5 gün", "Streak", "Bronze", null, "🔥", "streak",
            "{\"count\":5}", null, null, false, 10, true, 1, Actor, DateTime.UtcNow));
        Assert.ThrowsAny<ArgumentException>(() => badge.Update("Seri", "5 gün", "Streak", "Bronze", null, "🔥", "streak",
            "{\"days\":5}", null, null, true, 10, true, 1, Actor, DateTime.UtcNow));
    }

    [Fact]
    public async Task Students_do_not_see_badges_their_age_group_cannot_earn_unless_already_earned()
    {
        await using var db = Context();
        var adult = Guid.NewGuid(); var adultAge = Guid.NewGuid(); var childAge = Guid.NewGuid();
        var vocabularyType = ExerciseType.Create(Guid.NewGuid(), "Vocabulary", "Kelime", "vocabulary_builder");
        var vocabulary = Exercise.Create("Kelime", "vocabulary", "{}", 1, adult, vocabularyType.Id);
        typeof(Exercise).GetProperty(nameof(Exercise.TargetAgeGroupId))!.SetValue(vocabulary, childAge);
        db.ExerciseTypes.Add(vocabularyType); db.Exercises.Add(vocabulary);
        var words = Achievement.Create(Guid.NewGuid(), "Kelime Avcısı", "10 kelime", "Special", "Bronze", null, "📚",
            "vocabulary_learned", "{\"count\":10}", null, null, false, 10, true, 1);
        var streak = Achievement.Create(Guid.NewGuid(), "Seri", "3 gün", "Streak", "Bronze", null, "🔥", "streak",
            "{\"days\":3}", null, null, false, 10, true, 2);
        db.Achievements.AddRange(words, streak);
        db.UserProfiles.Add(Profile(adult, adultAge));
        await db.SaveChangesAsync();
        var gamification = Gamification(db);

        Assert.Equal(["Seri"], (await gamification.GetAchievementsAsync(adult)).Select(item => item.Name));

        db.UserAchievements.Add(UserAchievement.Import(Guid.NewGuid(), adult, words.Id, DateTime.UtcNow, false, null, false, null,
            null, DateTime.UtcNow, adult.ToString(), null, null));
        await db.SaveChangesAsync();
        Assert.Equal(["Kelime Avcısı", "Seri"], (await gamification.GetAchievementsAsync(adult)).Select(item => item.Name));
    }

    // ---- HIGH 2: the rebuild follows the live rules ----

    [Fact]
    public async Task Recalculation_ignores_legacy_rows_the_live_code_never_counted_and_pays_one_completion_a_day()
    {
        await using var db = Context();
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Comprehension", "Anlama", "reading_comprehension");
        var exercise = Exercise.Create("Anlama", "comprehension", "{}", 1, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        var day = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        var first = Result(student, exercise.Id, day, measured: true); first.RecordGamification(true, 40);
        var replay = Result(student, exercise.Id, day.AddHours(1), measured: true); replay.RecordGamification(true, 40);
        var notMeasured = Result(student, exercise.Id, day.AddDays(1), measured: false);           // legacy, flag null
        var incomplete = Result(student, exercise.Id, day.AddDays(2), measured: true);             // legacy, flag null
        db.ExerciseSessions.Add(Session(incomplete, "{\"readingIncomplete\":true}"));
        db.ExerciseSessionResults.AddRange(first, replay, notMeasured, incomplete);
        await db.SaveChangesAsync();

        await new OwnedSpeedReadingGamificationRecalculation(db).RunAsync();

        var stats = await db.UserGamifications.SingleAsync(item => item.UserId == student);
        Assert.Equal(40, stats.TotalXP);
        Assert.Equal(2, stats.TotalActivitiesCompleted);
    }

    // ---- LOW: program streak and daily goal days ----

    [Fact]
    public void The_program_streak_counts_consecutive_days_on_which_a_program_day_was_finished()
    {
        var yesterday = new DateTime(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc);
        var today = yesterday.AddDays(1);
        var template = ProgramTemplate.Create("Program", "Açıklama", Guid.NewGuid(), 0, 100, "{}", 1, 4, 5, 1, 7,
            true, 1, 0, null, false, Actor, yesterday.AddDays(-1));
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), Guid.NewGuid(), template, 0, 0, Actor, yesterday.AddHours(-1));
        progress.ApplyExerciseCompletion(80, false, 1, 1, template, Actor, yesterday);
        // Two tasks today: the first one moves the last completion time to today, the second finishes the day.
        progress.ApplyExerciseCompletion(80, false, 1, 2, template, Actor, today.AddMinutes(-30));
        var result = progress.ApplyExerciseCompletion(80, false, 2, 2, template, Actor, today,
            previousDayCompletedAt: yesterday);

        Assert.True(result.DayCompleted);
        Assert.Equal(2, progress.CurrentStreak);
    }

    [Fact]
    public void Istanbul_day_starts_at_21_utc_and_daily_minutes_are_capped()
    {
        Assert.Equal(new DateTime(2026, 10, 29, 21, 0, 0, DateTimeKind.Utc),
            GamificationRules.IstanbulDayStartUtc(new DateTime(2026, 10, 29, 22, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 10, 29, 21, 0, 0, DateTimeKind.Utc),
            GamificationRules.IstanbulDayStartUtc(new DateTime(2026, 10, 30, 20, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(120, GamificationRules.ClampDailyMinutes(0, 500));
        Assert.Equal(24 * 60, GamificationRules.ClampDailyMinutes(24 * 60 - 10, 100));
        Assert.Equal(30, GamificationRules.ClampDailyMinutes(10, 20));
    }

    // ---- helpers ----

    private static UserGamification Stats() => UserGamification.CreateDefault(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, null);

    private static ExerciseSessionResult Result(Guid student, Guid exercise, DateTime completedAt, bool measured) =>
        ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), student, exercise, null, 200, 60, 200m, 80m, 160m, 80m,
            completedAt, "[]", "[]", false, measured);

    private static ExerciseSession Session(ExerciseSessionResult result, string state)
    {
        var session = ExerciseSession.Start(result.StudentId, result.ExerciseId, null, 1, result.CompletedAt.AddMinutes(-2), null);
        typeof(ExerciseSession).GetProperty(nameof(ExerciseSession.Id))!.SetValue(session, result.SessionId);
        session.SetState(state);
        return session;
    }

    private static SpeedReadingUserProfile Profile(Guid user, Guid ageGroup)
    {
        var profile = (SpeedReadingUserProfile)Activator.CreateInstance(typeof(SpeedReadingUserProfile), nonPublic: true)!;
        typeof(SpeedReadingUserProfile).GetProperty(nameof(SpeedReadingUserProfile.Id))!.SetValue(profile, Guid.NewGuid());
        typeof(SpeedReadingUserProfile).GetProperty(nameof(SpeedReadingUserProfile.UserId))!.SetValue(profile, user);
        typeof(SpeedReadingUserProfile).GetProperty(nameof(SpeedReadingUserProfile.AgeGroupConfigurationId))!.SetValue(profile, ageGroup);
        return profile;
    }

    private static async Task<Application.ExerciseSessions.ExerciseSessionResult> PlayTargetHunt(
        ISpeedReadingExerciseSessions service, Guid user, Guid id)
    {
        var started = await service.StartAsync(user, new() { ExerciseId = id });
        var items = started.InitialData.GetProperty("targetHuntItems").EnumerateArray().Select(item => item.GetString()!).ToArray();
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_start" });
        foreach (var index in Enumerable.Range(0, items.Length).Where(index => items[index] == Target))
            await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_tap", Index = index });
        return await service.CompleteAsync(user, started.SessionId, new());
    }

    private static string TargetHuntConfig() => JsonSerializer.Serialize(new { engineType = "target_hunt",
        engineConfig = new { target = Target, distractors = new[] { "d", "p" }, rows = 3, columns = 4, targetCount = 3, timeLimit = 60 } });

    private static async Task<Guid> SeedTargetHunt(OwnedSpeedReadingDbContext db, Guid user)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "TargetHunt", "Hedef Avı", "target_hunt");
        var exercise = Exercise.Create("Hedef Avı", "target_hunt", TargetHuntConfig(), 1, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        return exercise.Id;
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) => (ISpeedReadingExerciseSessions)Activator.CreateInstance(
        typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

    private static ILegacySpeedReadingGamification Gamification(OwnedSpeedReadingDbContext db)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingGamification")!;
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var arguments = constructor.GetParameters().Select(parameter => parameter.ParameterType == typeof(OwnedSpeedReadingDbContext)
            ? (object)db : null).ToArray();
        return (ILegacySpeedReadingGamification)constructor.Invoke(arguments);
    }
}
