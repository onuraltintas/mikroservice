using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingConcurrentProgramCompletionTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_final_slots_complete_the_program_once_without_lost_progress(bool retryAfterSave)
    {
        DbContextOptions<OwnedSpeedReadingDbContext> Options(bool delay = false)
        {
            var builder = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql(postgres.ConnectionString, options => options.EnableRetryOnFailure(2, TimeSpan.Zero, null));
            if (delay) builder.AddInterceptors(new LogReadDelay());
            return builder.Options;
        }
        await using var setup = new OwnedSpeedReadingDbContext(Options());
        // Only the disposable test container database is reset.
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var typeId = Guid.NewGuid();
            setup.ExerciseTypes.Add(ExerciseType.Create(typeId, "Fixation", "Fixation", "focus"));
            var exercises = Enumerable.Range(0, 2).Select(_ => Exercise.Create("Exercise", "Fixation", "{}", 1, user, typeId)).ToArray();
            setup.Exercises.AddRange(exercises);
            var template = ProgramTemplate.Import(Guid.NewGuid(), "Program", "", Guid.NewGuid(), 0, 100,
                "{}", 1, 2, 5, 1, 1, true, 1, 0, null, false, now, null, null, null);
            setup.ProgramTemplates.Add(template);
            var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now);
            progress.SetSchedule(JsonSerializer.Serialize(exercises.Select((exercise, index) => new
            {
                weekNumber = 1, dayNumber = 1, order = index + 1, exerciseId = exercise.Id
            })), user, now);
            setup.StudentProgramProgresses.Add(progress);
            var sessions = exercises.Select(exercise => ExerciseSession.Start(user, exercise.Id, null, 1, now, null)).ToArray();
            foreach (var session in sessions)
            {
                session.Complete(now.AddSeconds(1));
                setup.ExerciseSessions.Add(session);
                setup.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, user,
                    session.ExerciseId, null, 0, 1, 0, 0, 0, 0, now, isMeasured: false));
            }
            await setup.SaveChangesAsync();
            async Task Complete(int index)
            {
                var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>(Options(delay: true));
                if (retryAfterSave && index == 0) options.AddInterceptors(new FailOnceAfterSave());
                await using var db = new OwnedSpeedReadingDbContext(options.Options);
                var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                    "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDailyProgress")!;
                var service = (ISpeedReadingDailyProgress)Activator.CreateInstance(type, db, null)!;
                await service.CompleteExerciseAsync(user, new CompleteDailyExerciseRequest
                {
                    ExerciseId = exercises[index].Id, SessionId = sessions[index].Id
                }, sessions[index].Id.ToString(), CancellationToken.None);
            }
            await Task.WhenAll(Complete(0), Complete(1));
            await Complete(0);
            setup.ChangeTracker.Clear();
            var saved = await setup.StudentProgramProgresses.SingleAsync();
            Assert.False(saved.IsActive);
            Assert.NotNull(saved.CompletedDate);
            Assert.Equal(1, saved.DaysCompleted);
            Assert.Equal(2, saved.ExercisesCompleted);
            Assert.Equal(2, await setup.DailyExerciseLogs.CountAsync());
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class FailOnceAfterSave : SaveChangesInterceptor
    {
        private bool failed;

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (!failed)
            {
                failed = true;
                throw new NpgsqlException("Injected transient failure before commit", new TimeoutException());
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class LogReadDelay : DbCommandInterceptor
    {
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM speed_reading.daily_exercise_logs")
                && command.CommandText.Contains("WHERE d.\"StudentProgramProgressId\"")
                && !command.CommandText.Contains("ORDER BY"))
                await Task.Delay(300, cancellationToken);
            return result;
        }
    }
}
