using System.Data.Common;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingBaselineTrainingGatePostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_program_assignment_blocks_baseline_and_old_form_session(bool exerciseSession)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var age = AgeGroupConfiguration.Create(Guid.NewGuid(), "teen", "Genç", 13, 16,
                100, 200, 300, 70, 15, 1, 1, true, null, user, now);
            var template = ProgramTemplate.Import(Guid.NewGuid(), "Training", "", age.Id, 0, 100,
                "{}", 1, 1, 5, 1, 1, true, 1, 0, null, false, now, null, null, null);
            var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Sabitleme", "motion_path");
            const string config = """{"engineType":"motion_path","engineConfig":{"mode":"fixation","content":{"points":2,"peripheralCount":2},"timing":{"holdMs":100}}}""";
            var exercise = Exercise.Create("Sabitleme", "attention", config, 1, user, type.Id);
            var baseline = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.Baseline,
                "baseline", "tr", null, 1, now, user.ToString());
            var snapshot = JsonSerializer.Serialize(new { version = 1,
                exercise = new { id = exercise.Id, title = exercise.Title, description = "attention", typeName = "Fixation",
                    difficultyLevel = 1, engineType = "motion_path", configurationJson = config }, questions = Array.Empty<object>() });
            db.AgeGroupConfigurations.Add(age); db.ProgramTemplates.Add(template);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.AssessmentAttempts.Add(baseline);
            db.AssessmentAttemptExercises.Add(AssessmentAttemptExercise.Pin(Guid.NewGuid(), baseline.Id, exercise.Id,
                null, "attention", 1, snapshot, now, user.ToString()));
            await db.SaveChangesAsync();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var bytes = user.ToByteArray();
            var key = BitConverter.ToInt64(bytes, 0) ^ BitConverter.ToInt64(bytes, 8);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
            var observer = new LockObserver();
            await using var contender = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql(postgres.ConnectionString).AddInterceptors(observer).Options);
            var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
            Task pending;
            if (exerciseSession)
            {
                var sessions = (ISpeedReadingExerciseSessions)Activator.CreateInstance(assembly.GetType(
                    "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, contender)!;
                pending = sessions.StartAsync(user, new() { ExerciseId = exercise.Id, AssessmentAttemptId = baseline.Id });
            }
            else
            {
                var assessment = (ISpeedReadingAssessment)Activator.CreateInstance(assembly.GetType(
                    "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!, contender, null)!;
                pending = assessment.StartAttemptAsync(user, new() { Phase = AssessmentAttemptPhase.Baseline }, CancellationToken.None);
            }
            try
            {
                await observer.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                db.StudentProgramProgresses.Add(StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now));
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                var error = await Assert.ThrowsAsync<BusinessRuleException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.Equal("Assessment.BaselineDuringTraining", error.Code);
                Assert.Equal(1, await db.AssessmentAttempts.CountAsync());
                Assert.Equal(0, await db.ExerciseSessions.CountAsync());
                Assert.True((await db.StudentProgramProgresses.SingleAsync()).IsActive);
            }
            finally
            {
                await transaction.DisposeAsync();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(10)); } catch { /* Expected rejection asserted above. */ }
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class LockObserver : DbCommandInterceptor
    {
        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) Waiting.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}
