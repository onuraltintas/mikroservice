using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;
using ExerciseSessionStatus = SpeedReading.Domain.Sessions.ExerciseSessionStatus;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Decision 12.11.2026: the recorded exercise time counts at most five minutes for any stretch without server
/// activity. Pauses stay excluded as before, exercises with their own elapsed counters and the time limit are unchanged.
/// </summary>
public sealed class SpeedReadingActiveTimeSessionTests
{
    private const string Tachistoscope = """{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"number","count":3,"length":3},"timing":{"durationMs":500,"intervalMs":300},"adaptive":{"enabled":false}},"difficultyLevel":1}""";

    [Fact]
    public async Task A_gap_under_the_cap_counts_in_full()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);

        await Round(db, service, user, id, 0);
        await Rewind(db, id, 200);
        await Round(db, service, user, id, 1);
        await Round(db, service, user, id, 2);

        var result = await service.CompleteAsync(user, id, new());
        result.TimeSpentSeconds.Should().BeInRange(206, 208);
        (await State(db, id))["idleExcessMilliseconds"]!.GetValue<long>().Should().Be(0);
    }

    [Fact]
    public async Task A_gap_over_the_cap_counts_as_the_cap()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);

        await Round(db, service, user, id, 0);
        await Rewind(db, id, 5 * 24 * 3600);
        await Round(db, service, user, id, 1);
        await Round(db, service, user, id, 2);

        var result = await service.CompleteAsync(user, id, new());
        result.TimeSpentSeconds.Should().BeInRange(306, 308);
        (await db.ExerciseSessionResults.SingleAsync()).TimeSpentSeconds.Should().Be(result.TimeSpentSeconds);
    }

    [Fact]
    public async Task Each_long_gap_is_capped_on_its_own()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);

        await Round(db, service, user, id, 0);
        await Rewind(db, id, 1_000);
        await Round(db, service, user, id, 1);
        await Rewind(db, id, 400);
        await Round(db, service, user, id, 2);

        (await service.CompleteAsync(user, id, new())).TimeSpentSeconds.Should().BeInRange(606, 608);
    }

    [Fact]
    public async Task Completing_long_after_the_last_round_adds_only_the_cap()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);

        for (var round = 0; round < 3; round++) await Round(db, service, user, id, round);
        await Rewind(db, id, 7_200);

        (await service.CompleteAsync(user, id, new())).TimeSpentSeconds.Should().BeInRange(306, 308);
    }

    [Fact]
    public async Task A_pause_is_still_excluded_in_full_and_is_not_capped_twice()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);

        await Round(db, service, user, id, 0);
        await Rewind(db, id, 100);
        await service.PauseAsync(user, id);
        await Rewind(db, id, 9_000);
        await service.ResumeAsync(user, id);
        await Rewind(db, id, 100);
        await Round(db, service, user, id, 1);
        await Round(db, service, user, id, 2);

        var result = await service.CompleteAsync(user, id, new());
        result.TimeSpentSeconds.Should().BeInRange(206, 208);
        (await State(db, id))["idleExcessMilliseconds"]!.GetValue<long>().Should().Be(0);
    }

    [Fact]
    public async Task A_pause_that_arrives_after_a_long_gap_and_a_late_resume_count_the_cap_once()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);

        await Round(db, service, user, id, 0);
        await Rewind(db, id, 3_000);          // away with the exercise still running
        await service.PauseAsync(user, id);   // the pause reaches the server on return
        await Rewind(db, id, 50_000);
        await service.ResumeAsync(user, id);
        await Rewind(db, id, 20);
        await Round(db, service, user, id, 1);
        await Round(db, service, user, id, 2);

        (await service.CompleteAsync(user, id, new())).TimeSpentSeconds.Should().BeInRange(326, 328);
    }

    [Fact]
    public async Task A_repeated_action_does_not_count_the_gap_twice()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);

        await Round(db, service, user, id, 0);
        await Rewind(db, id, 1_000);
        var actionId = Guid.NewGuid();
        var first = await service.ValidateActionAsync(user, id, new() { ActionId = actionId, Action = "tachistoscope_present", Index = 1 });
        var idle = (await State(db, id))["idleExcessMilliseconds"]!.GetValue<long>();
        var retry = await service.ValidateActionAsync(user, id, new() { ActionId = actionId, Action = "tachistoscope_present", Index = 1 });
        await service.ValidateActionAsync(user, id, new() { Action = "tachistoscope_present", Index = 1 });

        idle.Should().BeInRange(700_000, 701_000);
        retry.FeedbackData!.Value.GetProperty("stimulus").GetString().Should().Be(first.FeedbackData!.Value.GetProperty("stimulus").GetString());
        (await State(db, id))["idleExcessMilliseconds"]!.GetValue<long>().Should().Be(idle);
    }

    [Fact]
    public async Task A_long_idle_read_no_longer_lowers_the_reading_speed()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var exercise = await SeedFreeReading(db, user);
        db.ReadingTexts.Add(Text(exercise, 200)); await db.SaveChangesAsync();
        var service = Service(db); var started = await service.StartAsync(user, new() { ExerciseId = exercise });

        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "start_reading" });
        await Rewind(db, started.SessionId, 1_200);
        var finished = await service.ValidateActionAsync(user, started.SessionId, new() { Action = "finish_reading" });
        var result = await service.CompleteAsync(user, started.SessionId, new());

        finished.IsValid.Should().BeTrue();
        // 200 words in the 300 counted seconds; the uncapped 1200 seconds gave 10 WPM, which is not a valid speed.
        result.RawWPM.Should().BeInRange(39.9m, 40m);
        finished.CurrentWPM.Should().Be(result.RawWPM);
        result.TimeSpentSeconds.Should().BeInRange(300, 301);
        result.DetailedResults.GetProperty("readingIdleExcessMilliseconds").GetInt64().Should().BeInRange(900_000, 901_000);
    }

    [Fact]
    public async Task A_read_within_the_cap_keeps_its_measured_speed()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var exercise = await SeedFreeReading(db, user);
        db.ReadingTexts.Add(Text(exercise, 200)); await db.SaveChangesAsync();
        var service = Service(db); var started = await service.StartAsync(user, new() { ExerciseId = exercise });

        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "start_reading" });
        await Rewind(db, started.SessionId, 200);
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "finish_reading" });
        var result = await service.CompleteAsync(user, started.SessionId, new());

        result.RawWPM.Should().BeInRange(59.9m, 60m);
        result.TimeSpentSeconds.Should().BeInRange(200, 201);
    }

    [Fact]
    public async Task A_long_text_may_be_read_in_one_step_for_longer_than_the_cap()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var exercise = await SeedFreeReading(db, user);
        db.ReadingTexts.Add(Text(exercise, 400)); await db.SaveChangesAsync();
        var service = Service(db); var started = await service.StartAsync(user, new() { ExerciseId = exercise });

        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "start_reading" });
        await Rewind(db, started.SessionId, 500);
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "finish_reading" });
        var result = await service.CompleteAsync(user, started.SessionId, new());

        // 400 words may take ten minutes (40 words per minute) before the gap is treated as idle.
        result.TimeSpentSeconds.Should().BeInRange(500, 501);
        result.RawWPM.Should().BeInRange(47.9m, 48m);
    }

    [Fact]
    public async Task The_question_phase_does_not_change_the_reading_time()
    {
        await using var db = Context(); var user = Guid.NewGuid(); var exercise = await SeedFreeReading(db, user);
        db.ReadingTexts.Add(Text(exercise, 200)); await db.SaveChangesAsync();
        var service = Service(db); var started = await service.StartAsync(user, new() { ExerciseId = exercise });

        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "start_reading" });
        await Rewind(db, started.SessionId, 100);
        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "finish_reading" });
        await Rewind(db, started.SessionId, 4_000);
        var result = await service.CompleteAsync(user, started.SessionId, new());

        result.TimeSpentSeconds.Should().BeInRange(100, 101);
        result.RawWPM.Should().BeInRange(119.8m, 120m);
    }

    [Fact]
    public async Task An_exercise_with_its_own_elapsed_counter_is_unchanged()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "TargetHunt", "Hedef Avı", "target_hunt");
        var exercise = Exercise.Create("Hedef Avı", "target_hunt", JsonSerializer.Serialize(new { engineType = "target_hunt",
            engineConfig = new { target = "b", distractors = new[] { "d", "p" }, rows = 3, columns = 4, targetCount = 3, timeLimit = 60 } }), 1, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var service = Service(db); var started = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
        var items = started.InitialData.GetProperty("targetHuntItems").EnumerateArray().Select(item => item.GetString()).ToArray();

        await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_start" });
        // The last server activity is long ago, but the round's own clock shows 30 seconds.
        var session = await db.ExerciseSessions.SingleAsync(); var state = JsonNode.Parse(session.SessionDataJson)!;
        state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-30);
        state["lastActivityAt"] = DateTime.UtcNow.AddSeconds(-5_000);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        foreach (var index in Enumerable.Range(0, items.Length).Where(index => items[index] == "b"))
            await service.ValidateActionAsync(user, started.SessionId, new() { Action = "target_hunt_tap", Index = index });
        var result = await service.CompleteAsync(user, started.SessionId, new());

        result.TimeSpentSeconds.Should().BeInRange(30, 31);
        result.Score.Should().Be(100m);
    }

    [Fact]
    public async Task A_timed_session_still_times_out_after_an_idle_gap()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);
        await Round(db, service, user, id, 0);
        var session = await db.ExerciseSessions.SingleAsync();
        typeof(ExerciseSession).GetProperty(nameof(ExerciseSession.TimeLimitSeconds))!.SetValue(session, 600);
        await db.SaveChangesAsync();
        await Rewind(db, id, 1_000);

        // 1000 idle seconds count as 300 seconds of study time, but the 600-second limit has still passed.
        var action = () => service.ValidateActionAsync(user, id, new() { Action = "tachistoscope_present", Index = 1 });

        (await action.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("süresi doldu");
        db.ChangeTracker.Clear();
        (await db.ExerciseSessions.SingleAsync()).Status.Should().Be(ExerciseSessionStatus.Timeout);
    }

    [Fact]
    public async Task A_focus_exercise_left_idle_counts_the_cap()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartFocus(db, service, user, totalSteps: 3);

        (await service.ValidateActionAsync(user, id, new() { Action = "focus_start" })).IsValid.Should().BeTrue();
        await Rewind(db, id, 1_000);
        (await service.ValidateActionAsync(user, id, new() { Action = "complete" })).IsValid.Should().BeTrue();
        var result = await service.CompleteAsync(user, id, new());

        result.TimeSpentSeconds.Should().BeInRange(300, 301);
        (await State(db, id))["focusIdleExcessMilliseconds"]!.GetValue<long>().Should().BeInRange(700_000, 701_000);
    }

    [Fact]
    public async Task A_focus_exercise_within_the_cap_keeps_its_own_time()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartFocus(db, service, user, totalSteps: 3);

        await service.ValidateActionAsync(user, id, new() { Action = "focus_start" });
        await Rewind(db, id, 40);
        (await service.ValidateActionAsync(user, id, new() { Action = "complete" })).IsValid.Should().BeTrue();

        (await service.CompleteAsync(user, id, new())).TimeSpentSeconds.Should().BeInRange(40, 41);
    }

    [Fact]
    public async Task The_focus_clock_stops_when_the_exercise_is_finished()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartFocus(db, service, user, totalSteps: 3);

        await service.ValidateActionAsync(user, id, new() { Action = "focus_start" });
        await Rewind(db, id, 4);
        (await service.ValidateActionAsync(user, id, new() { Action = "complete" })).IsValid.Should().BeTrue();
        // The result is saved two hours after the exercise ended.
        await Rewind(db, id, 7_200);

        (await service.CompleteAsync(user, id, new())).TimeSpentSeconds.Should().BeInRange(4, 5);
    }

    [Fact]
    public async Task A_focus_stream_that_runs_longer_than_the_cap_without_taps_keeps_its_time()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartFocus(db, service, user, totalSteps: 400);

        await service.ValidateActionAsync(user, id, new() { Action = "focus_start" });
        await Rewind(db, id, 400);
        (await service.ValidateActionAsync(user, id, new() { Action = "complete" })).IsValid.Should().BeTrue();

        // 400 one-second stimuli: the run itself takes 400 seconds, with no step in between.
        (await service.CompleteAsync(user, id, new())).TimeSpentSeconds.Should().BeInRange(400, 401);
        (await State(db, id))["focusIdleExcessMilliseconds"]!.GetValue<long>().Should().BeLessThan(1_000);
    }

    [Fact]
    public async Task A_session_from_before_this_change_is_capped_when_it_is_completed()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);
        for (var round = 0; round < 3; round++) await Round(db, service, user, id, round);

        // Such a session has no activity point in its state; the row's last update is the fallback.
        await Rewind(db, id, 7_200, legacy: true);
        var result = await service.CompleteAsync(user, id, new()
        {
            CustomData = new() { ["score"] = JsonSerializer.SerializeToElement(100) }
        });

        result.TimeSpentSeconds.Should().BeInRange(306, 308);
    }

    [Fact]
    public async Task A_session_from_before_this_change_is_capped_on_its_next_step()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);
        await Round(db, service, user, id, 0);
        await Rewind(db, id, 7_200, legacy: true);
        await Round(db, service, user, id, 1);
        await Round(db, service, user, id, 2);

        (await service.CompleteAsync(user, id, new())).TimeSpentSeconds.Should().BeInRange(306, 308);
    }

    [Fact]
    public async Task A_pause_reported_after_the_stimulus_was_shown_gives_a_new_stimulus_on_resume()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var service = Service(db); var id = await StartTachistoscope(db, service, user);

        var shown = await service.ValidateActionAsync(user, id, new() { Action = "tachistoscope_present", Index = 0 });
        // The automatic pause did not reach the server; the player reports it just before it resumes.
        await service.PauseAsync(user, id);
        await service.ResumeAsync(user, id);
        var again = await service.ValidateActionAsync(user, id, new() { Action = "tachistoscope_present", Index = 0 });

        again.IsValid.Should().BeTrue();
        again.FeedbackData!.Value.GetProperty("round").GetInt32().Should().Be(0);
        again.FeedbackData!.Value.GetProperty("stimulus").GetString()
            .Should().NotBe(shown.FeedbackData!.Value.GetProperty("stimulus").GetString());
    }

    private static async Task<Guid> StartFocus(OwnedSpeedReadingDbContext db, ISpeedReadingExerciseSessions service, Guid user, int totalSteps)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "Focus", "Odaklanma", "focus");
        var exercise = Exercise.Create("Odaklanma", "focus",
            $$$"""{"engineType":"focus","engineConfig":{"mode":"position","nLevel":1,"speedMs":1000,"totalSteps":{{{totalSteps}}},"gridSize":3}}""", 3, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var started = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
        started.TotalSteps.Should().Be(totalSteps);
        return started.SessionId;
    }

    private static async Task<Guid> StartTachistoscope(OwnedSpeedReadingDbContext db, ISpeedReadingExerciseSessions service, Guid user)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "Tachistoscope", "Takistoskop", "text_stream");
        var exercise = Exercise.Create("Takistoskop", "Tachistoscope", Tachistoscope, 1, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var started = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
        started.TotalSteps.Should().Be(3);
        return started.SessionId;
    }

    /// <summary>One validated round that takes two seconds of server time.</summary>
    private static async Task Round(OwnedSpeedReadingDbContext db, ISpeedReadingExerciseSessions service, Guid user, Guid id, int round)
    {
        var presented = await service.ValidateActionAsync(user, id, new() { Action = "tachistoscope_present", Index = round });
        presented.IsValid.Should().BeTrue(presented.Message);
        await Rewind(db, id, 2);
        var answered = await service.ValidateActionAsync(user, id, new()
        {
            Action = "tachistoscope_answer", Index = round,
            Answer = presented.FeedbackData!.Value.GetProperty("stimulus").GetString()
        });
        answered.IsValid.Should().BeTrue(answered.Message);
    }

    /// <summary>Moves every clock of the open session back, as if that much time had passed since its last activity.</summary>
    private static async Task Rewind(OwnedSpeedReadingDbContext db, Guid id, double seconds, bool legacy = false)
    {
        db.ChangeTracker.Clear();
        var session = await db.ExerciseSessions.SingleAsync(item => item.Id == id);
        var state = JsonNode.Parse(session.SessionDataJson)!.AsObject();
        foreach (var key in new[] { "timingStartedAt", "lastActivityAt", "readingStartTime", "readingEndTime", "readingPausedAt",
            "focusStartTime", "focusEndTime", "focusPresentedAt", "focusPausedAt" })
            Shift(state, key, seconds);
        if (legacy) state.Remove("lastActivityAt");
        if (state["tachistoscope"] is JsonObject tachistoscope) Shift(tachistoscope, "presentedAt", seconds);
        session.SetState(state.ToJsonString(), session.CustomDataJson);
        foreach (var name in new[] { nameof(ExerciseSession.StartTime), nameof(ExerciseSession.PausedAt) })
        {
            var property = typeof(ExerciseSession).GetProperty(name)!;
            if (property.GetValue(session) is DateTime value) property.SetValue(session, value.AddSeconds(-seconds));
        }
        // SetState stamps the row; a session from before this change only has that stamp to go by.
        if (legacy)
            typeof(ExerciseSession).GetProperty(nameof(ExerciseSession.UpdatedAt))!.SetValue(session, DateTime.UtcNow.AddSeconds(-seconds));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static void Shift(JsonObject node, string key, double seconds)
    {
        if (node[key] is JsonValue value && value.TryGetValue<DateTime>(out var time))
            node[key] = time.ToUniversalTime().AddSeconds(-seconds);
    }

    private static async Task<JsonObject> State(OwnedSpeedReadingDbContext db, Guid id)
    {
        db.ChangeTracker.Clear();
        return JsonNode.Parse((await db.ExerciseSessions.SingleAsync(item => item.Id == id)).SessionDataJson)!.AsObject();
    }

    private static ReadingText Text(Guid exercise, int count) => ReadingText.Create(Guid.NewGuid(), "Metin",
        string.Join(" ", Enumerable.Repeat("kelime", count)), difficultyLevel: 2, exerciseId: exercise);

    private static async Task<Guid> SeedFreeReading(OwnedSpeedReadingDbContext db, Guid user)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "FreeReading", "Serbest Okuma", "free_reading");
        var exercise = Exercise.Create("Serbest Okuma", "free_reading", "{\"engineType\":\"free_reading\",\"engineConfig\":{\"content\":{\"minWordCount\":50,\"maxWordCount\":500},\"timing\":{\"minReadingTimeMs\":3000}}}", 2, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync(); return exercise.Id;
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) => (ISpeedReadingExerciseSessions)Activator.CreateInstance(
        typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
}
