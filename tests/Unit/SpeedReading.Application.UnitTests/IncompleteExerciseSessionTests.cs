using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;
using OwnedSessionResult = SpeedReading.Domain.Sessions.ExerciseSessionResult;

namespace SpeedReading.Application.UnitTests;

public sealed class IncompleteExerciseSessionTests
{
    [Fact]
    public async Task Discarding_one_session_deletes_its_answers_and_preserves_other_owned_and_completed_sessions()
    {
        await using var db = Context();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var exercise = await AddExercise(db, studentId);
        var assessment = AssessmentAttempt.Start(Guid.NewGuid(), studentId, AssessmentAttemptPhase.Baseline,
            "form-v1", "tr", null, 1, DateTime.UtcNow, studentId.ToString());
        var active = ExerciseSession.Start(studentId, exercise.Id, null, 10, DateTime.UtcNow, null,
            assessmentAttemptId: assessment.Id);
        active.RecordAnswer(Guid.NewGuid(), "A", true, 1, 0);
        var paused = NewSession(studentId, exercise.Id);
        paused.Pause(DateTime.UtcNow);
        var completed = NewSession(studentId, exercise.Id);
        completed.Complete(DateTime.UtcNow);
        var otherStudentSession = NewSession(otherStudentId, exercise.Id);
        db.ExerciseSessions.AddRange(active, paused, completed, otherStudentSession);
        db.AssessmentAttempts.Add(assessment);
        var completedResult = OwnedSessionResult.Create(Guid.NewGuid(), completed.Id, studentId,
            exercise.Id, null, 100, 1, 100, 100, 100, 100, DateTime.UtcNow);
        db.ExerciseSessionResults.Add(completedResult);
        await db.SaveChangesAsync();

        await Service(db).DiscardIncompleteAsync(studentId, active.Id);

        Assert.False(await db.ExerciseSessions.AnyAsync(item => item.Id == active.Id));
        Assert.False(await db.ExerciseSessionAnswers.AnyAsync(item => item.SessionId == active.Id));
        Assert.True(await db.ExerciseSessions.AnyAsync(item => item.Id == paused.Id));
        Assert.True(await db.ExerciseSessions.AnyAsync(item => item.Id == completed.Id));
        Assert.True(await db.ExerciseSessions.AnyAsync(item => item.Id == otherStudentSession.Id));
        Assert.True(await db.ExerciseSessionResults.AnyAsync(item => item.Id == completedResult.Id));
        Assert.True(await db.AssessmentAttempts.AnyAsync(item => item.Id == assessment.Id
            && item.Status == AssessmentAttemptStatus.InProgress));
    }

    [Fact]
    public async Task Discarding_all_removes_only_the_current_students_active_and_paused_sessions()
    {
        await using var db = Context();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var exercise = await AddExercise(db, studentId);
        var active = NewSession(studentId, exercise.Id);
        var paused = NewSession(studentId, exercise.Id);
        paused.Pause(DateTime.UtcNow);
        var completed = NewSession(studentId, exercise.Id);
        completed.Complete(DateTime.UtcNow);
        var otherStudentSession = NewSession(otherStudentId, exercise.Id);
        db.ExerciseSessions.AddRange(active, paused, completed, otherStudentSession);
        await db.SaveChangesAsync();

        await Service(db).DiscardAllIncompleteAsync(studentId);

        Assert.False(await db.ExerciseSessions.AnyAsync(item => item.Id == active.Id));
        Assert.False(await db.ExerciseSessions.AnyAsync(item => item.Id == paused.Id));
        Assert.True(await db.ExerciseSessions.AnyAsync(item => item.Id == completed.Id));
        Assert.True(await db.ExerciseSessions.AnyAsync(item => item.Id == otherStudentSession.Id));
    }

    [Fact]
    public async Task Starting_after_discard_creates_a_fresh_session_instead_of_resuming_partial_progress()
    {
        await using var db = Context();
        var studentId = Guid.NewGuid();
        var exercise = await AddExercise(db, studentId);
        var incomplete = NewSession(studentId, exercise.Id);
        incomplete.RecordAnswer(Guid.NewGuid(), "A", true, 1, 0);
        db.ExerciseSessions.Add(incomplete);
        await db.SaveChangesAsync();

        var service = Service(db);
        await service.DiscardIncompleteAsync(studentId, incomplete.Id);
        var restarted = await service.StartAsync(studentId, new() { ExerciseId = exercise.Id });

        Assert.NotEqual(incomplete.Id, restarted.SessionId);
        var stored = await db.ExerciseSessions.SingleAsync(item => item.Id == restarted.SessionId);
        Assert.Equal(0, stored.CurrentStep);
        Assert.Empty(await db.ExerciseSessionAnswers.Where(item => item.SessionId == restarted.SessionId).ToListAsync());
        Assert.Empty(await db.ExerciseSessionResults.ToListAsync());
    }

    private static async Task<Exercise> AddExercise(OwnedSpeedReadingDbContext db, Guid creatorId)
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "SchulteTable", "Schulte Tablosu", "grid_interaction");
        var exercise = Exercise.Create("Test egzersizi", "attention", "{}", 1, creatorId, type.Id);
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return exercise;
    }

    private static ExerciseSession NewSession(Guid studentId, Guid exerciseId) =>
        ExerciseSession.Start(studentId, exerciseId, null, 10, DateTime.UtcNow, null);

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
}
