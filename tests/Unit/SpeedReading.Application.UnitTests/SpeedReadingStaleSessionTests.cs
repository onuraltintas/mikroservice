using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Configuration;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Decision 12.11.2026: an exercise session that was left open (active or paused) and saw no activity for a day is
/// closed as abandoned. Its end time is its last activity plus the idle cap, never the time of the clean-up.
/// </summary>
public sealed class SpeedReadingStaleSessionTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_threshold_defaults_to_one_day_and_must_be_positive()
    {
        new SpeedReadingServiceOptions().StaleSessionHours.Should().Be(24);

        var invalid = () => new SpeedReadingServiceOptions { StaleSessionHours = 0 }.Validate();
        invalid.Should().Throw<InvalidOperationException>().WithMessage("*StaleSessionHours*");
    }

    [Fact]
    public async Task Only_open_sessions_older_than_the_threshold_are_abandoned()
    {
        await using var db = Context();
        var staleActive = Session(ExerciseSessionStatus.Active, Now.AddDays(-5), lastActivity: Now.AddDays(-5).AddMinutes(3));
        var stalePaused = Session(ExerciseSessionStatus.Paused, Now.AddHours(-30), lastActivity: Now.AddHours(-29), pausedAt: Now.AddHours(-29));
        var recentActive = Session(ExerciseSessionStatus.Active, Now.AddHours(-23), lastActivity: Now.AddHours(-23));
        var recentPaused = Session(ExerciseSessionStatus.Paused, Now.AddHours(-30), lastActivity: Now.AddHours(-2), pausedAt: Now.AddHours(-2));
        var completed = Session(ExerciseSessionStatus.Completed, Now.AddDays(-9), lastActivity: Now.AddDays(-9), endTime: Now.AddDays(-9).AddMinutes(4));
        var timedOut = Session(ExerciseSessionStatus.Timeout, Now.AddDays(-9), lastActivity: Now.AddDays(-9), endTime: Now.AddDays(-9).AddMinutes(4));
        db.ExerciseSessions.AddRange(staleActive, stalePaused, recentActive, recentPaused, completed, timedOut);
        await db.SaveChangesAsync();

        var abandoned = await Processor(db).RunAsync(CancellationToken.None);

        abandoned.Should().Be(2);
        db.ChangeTracker.Clear();
        var rows = await db.ExerciseSessions.ToDictionaryAsync(item => item.Id);
        rows[staleActive.Id].Status.Should().Be(ExerciseSessionStatus.Abandoned);
        rows[stalePaused.Id].Status.Should().Be(ExerciseSessionStatus.Abandoned);
        rows[recentActive.Id].Status.Should().Be(ExerciseSessionStatus.Active);
        rows[recentPaused.Id].Status.Should().Be(ExerciseSessionStatus.Paused);
        rows[completed.Id].Status.Should().Be(ExerciseSessionStatus.Completed);
        rows[timedOut.Id].Status.Should().Be(ExerciseSessionStatus.Timeout);
        rows[completed.Id].EndTime.Should().Be(Now.AddDays(-9).AddMinutes(4));
    }

    [Fact]
    public async Task The_end_time_is_the_last_activity_plus_the_cap_not_the_clean_up_time()
    {
        await using var db = Context();
        var start = Now.AddDays(-5);
        var withState = Session(ExerciseSessionStatus.Active, start, lastActivity: start.AddMinutes(3));
        var legacy = Session(ExerciseSessionStatus.Active, start, lastActivity: null, updatedAt: start.AddMinutes(8));
        var neverTouched = Session(ExerciseSessionStatus.Active, start, lastActivity: null, updatedAt: null);
        var paused = Session(ExerciseSessionStatus.Paused, start, lastActivity: start.AddMinutes(6), pausedAt: start.AddMinutes(6), totalPausedSeconds: 40);
        db.ExerciseSessions.AddRange(withState, legacy, neverTouched, paused);
        await db.SaveChangesAsync();

        await Processor(db).RunAsync(CancellationToken.None);

        db.ChangeTracker.Clear();
        var rows = await db.ExerciseSessions.ToDictionaryAsync(item => item.Id);
        rows[withState.Id].EndTime.Should().Be(start.AddMinutes(8));
        rows[legacy.Id].EndTime.Should().Be(start.AddMinutes(13));
        rows[neverTouched.Id].EndTime.Should().Be(start.AddMinutes(5));
        // A paused session did no work after its pause: it ends there and gains no paused time.
        rows[paused.Id].EndTime.Should().Be(start.AddMinutes(6));
        rows[paused.Id].TotalPausedSeconds.Should().Be(40);
        rows[paused.Id].PausedAt.Should().BeNull();
        rows.Values.Should().OnlyContain(item => item.EndTime < Now.AddDays(-4));
    }

    [Fact]
    public async Task Assessment_sessions_are_left_alone_and_other_sessions_are_not()
    {
        await using var db = Context();
        var start = Now.AddDays(-3);
        // The (assessment attempt, exercise) pair is unique, so an abandoned assessment exercise could never be restarted.
        var assessment = Session(ExerciseSessionStatus.Active, start, lastActivity: start, assessmentAttemptId: Guid.NewGuid());
        var pausedAssessment = Session(ExerciseSessionStatus.Paused, start, lastActivity: start, pausedAt: start, assessmentAttemptId: Guid.NewGuid());
        var assignment = Session(ExerciseSessionStatus.Active, start, lastActivity: start, studentAssignmentId: Guid.NewGuid());
        var exam = Session(ExerciseSessionStatus.Active, start, lastActivity: start, engineType: "exam_simulation");
        db.ExerciseSessions.AddRange(assessment, pausedAssessment, assignment, exam);
        await db.SaveChangesAsync();

        (await Processor(db).RunAsync(CancellationToken.None)).Should().Be(2);

        db.ChangeTracker.Clear();
        var rows = await db.ExerciseSessions.ToDictionaryAsync(item => item.Id);
        rows[assessment.Id].Status.Should().Be(ExerciseSessionStatus.Active);
        rows[pausedAssessment.Id].Status.Should().Be(ExerciseSessionStatus.Paused);
        rows[assignment.Id].Status.Should().Be(ExerciseSessionStatus.Abandoned);
        rows[assignment.Id].StudentAssignmentId.Should().Be(assignment.StudentAssignmentId);
        rows[exam.Id].Status.Should().Be(ExerciseSessionStatus.Abandoned);
    }

    [Fact]
    public async Task Sessions_are_processed_in_batches_and_a_second_run_changes_nothing()
    {
        await using var db = Context();
        var count = SpeedReading.Infrastructure.SpeedReadingStaleSessionProcessor.BatchSize * 2 + 7;
        for (var index = 0; index < count; index++)
            db.ExerciseSessions.Add(Session(ExerciseSessionStatus.Active, Now.AddDays(-2), lastActivity: Now.AddDays(-2).AddSeconds(index)));
        var fresh = Session(ExerciseSessionStatus.Active, Now.AddHours(-1), lastActivity: Now.AddHours(-1));
        db.ExerciseSessions.Add(fresh);
        await db.SaveChangesAsync();

        (await Processor(db).RunAsync(CancellationToken.None)).Should().Be(count);
        db.ChangeTracker.Clear();
        var firstRun = await db.ExerciseSessions.AsNoTracking().ToDictionaryAsync(item => item.Id, item => (item.Status, item.EndTime, item.UpdatedAt));

        (await Processor(db).RunAsync(CancellationToken.None)).Should().Be(0);
        db.ChangeTracker.Clear();
        var secondRun = await db.ExerciseSessions.AsNoTracking().ToDictionaryAsync(item => item.Id, item => (item.Status, item.EndTime, item.UpdatedAt));

        secondRun.Should().BeEquivalentTo(firstRun);
        firstRun.Values.Count(item => item.Status == ExerciseSessionStatus.Abandoned).Should().Be(count);
        firstRun[fresh.Id].Status.Should().Be(ExerciseSessionStatus.Active);
    }

    [Fact]
    public async Task The_threshold_is_configurable()
    {
        await using var db = Context();
        var session = Session(ExerciseSessionStatus.Active, Now.AddHours(-30), lastActivity: Now.AddHours(-30));
        db.ExerciseSessions.Add(session);
        await db.SaveChangesAsync();

        (await Processor(db, staleSessionHours: 48).RunAsync(CancellationToken.None)).Should().Be(0);
        (await Processor(db, staleSessionHours: 24).RunAsync(CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task The_student_can_start_the_exercise_again_after_the_clean_up()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "TargetHunt", "Hedef Avı", "target_hunt");
        var exercise = Exercise.Create("Hedef Avı", "target_hunt", JsonSerializer.Serialize(new { engineType = "target_hunt",
            engineConfig = new { target = "b", distractors = new[] { "d", "p" }, rows = 3, columns = 4, targetCount = 3, timeLimit = 60 } }), 1, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); await db.SaveChangesAsync();
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(
            typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var first = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
        (await service.StartAsync(user, new() { ExerciseId = exercise.Id })).SessionId.Should().Be(first.SessionId);

        // The clean-up runs two days later.
        (await new SpeedReading.Infrastructure.SpeedReadingStaleSessionProcessor(db, new FixedClock(DateTime.UtcNow.AddDays(2)),
            new SpeedReadingServiceOptions()).RunAsync(CancellationToken.None)).Should().Be(1);
        db.ChangeTracker.Clear();

        (await service.GetActiveAsync(user)).Should().BeEmpty();
        var second = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
        second.SessionId.Should().NotBe(first.SessionId);
        var abandoned = await db.ExerciseSessions.AsNoTracking().SingleAsync(item => item.Id == first.SessionId);
        abandoned.Status.Should().Be(ExerciseSessionStatus.Abandoned);
        abandoned.EndTime.Should().BeCloseTo(abandoned.StartTime.AddSeconds(300), TimeSpan.FromSeconds(5));
    }

    private static SpeedReading.Infrastructure.SpeedReadingStaleSessionProcessor Processor(OwnedSpeedReadingDbContext db, int staleSessionHours = 24) =>
        new(db, new FixedClock(Now), new SpeedReadingServiceOptions { StaleSessionHours = staleSessionHours });

    private static ExerciseSession Session(
        ExerciseSessionStatus status,
        DateTime start,
        DateTime? lastActivity,
        DateTime? pausedAt = null,
        DateTime? endTime = null,
        int totalPausedSeconds = 0,
        Guid? assessmentAttemptId = null,
        Guid? studentAssignmentId = null,
        string engineType = "visual_expansion",
        DateTime? updatedAt = null)
    {
        var state = lastActivity.HasValue
            ? JsonSerializer.Serialize(new { engineType, lastActivityAt = lastActivity.Value })
            : JsonSerializer.Serialize(new { engineType });
        return ExerciseSession.Import(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, studentAssignmentId, status, start, endTime,
            totalPausedSeconds, pausedAt, null, 1, 5, 1, 0, state, null, "{}", start, null,
            updatedAt ?? lastActivity, null, assessmentAttemptId);
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
