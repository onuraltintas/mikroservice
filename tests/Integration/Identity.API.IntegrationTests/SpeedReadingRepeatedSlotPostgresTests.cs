using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingRepeatedSlotPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repeated_exercise_slots_complete_independently_and_retries_do_not_double_count(bool concurrent)
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options;
        await using var setup = new OwnedSpeedReadingDbContext(options);
        await setup.Database.EnsureDeletedAsync();
        try
        {
            await setup.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Fixation", "focus");
            var exercise = Exercise.Create("Exercise", "Fixation", "{}", 1, user, type.Id);
            setup.ExerciseTypes.Add(type);
            setup.Exercises.Add(exercise);
            var template = ProgramTemplate.Import(Guid.NewGuid(), "Repeated program", "", Guid.NewGuid(), 0, 100,
                "{}", 1, 1, 5, 1, 1, true, 1, 0, null, false, now, null, null, null);
            setup.ProgramTemplates.Add(template);
            var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now);
            progress.SetSchedule(JsonSerializer.Serialize(Enumerable.Range(1, 2).Select(order => new
            { weekNumber = 1, dayNumber = 1, order, exerciseId = exercise.Id })), user, now);
            setup.StudentProgramProgresses.Add(progress);
            var sessions = Enumerable.Range(1, 2).Select(_ => ExerciseSession.Start(user, exercise.Id, null, 1, now, null)).ToArray();
            foreach (var session in sessions)
            {
                session.Complete(now.AddSeconds(1));
                setup.ExerciseSessions.Add(session);
                setup.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, user,
                    exercise.Id, null, 0, 1, 0, 0, 0, 0, now, isMeasured: false));
            }
            await setup.SaveChangesAsync();

            ISpeedReadingDailyProgress Service(OwnedSpeedReadingDbContext db) => (ISpeedReadingDailyProgress)Activator.CreateInstance(
                typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                    "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDailyProgress")!, db, null)!;
            async Task Complete(int index)
            {
                await using var db = new OwnedSpeedReadingDbContext(options);
                var request = JsonSerializer.Deserialize<CompleteDailyExerciseRequest>(JsonSerializer.Serialize(new
                {
                    ExerciseId = exercise.Id, SessionId = sessions[index].Id, SlotOrder = index + 1,
                    ProgramProgressId = progress.Id, ProgramDay = 1
                }))!;
                await Service(db).CompleteExerciseAsync(user, request, sessions[index].Id.ToString(), CancellationToken.None);
            }
            if (concurrent)
                await Task.WhenAll(Complete(0), Complete(1));
            else
            {
                await Complete(0);
                setup.ChangeTracker.Clear();
                Assert.True((await setup.StudentProgramProgresses.SingleAsync()).IsActive);
                var today = await Service(setup).GetTodayExercisesAsync(user);
                Assert.True(today.Single(item => item.Order == 1).IsCompleted);
                Assert.False(today.Single(item => item.Order == 2).IsCompleted);
                await Complete(0);
                Assert.Equal(1, await setup.DailyExerciseLogs.CountAsync());
                await Complete(1);
            }
            await Complete(0);
            await Complete(1);
            setup.ChangeTracker.Clear();
            var saved = await setup.StudentProgramProgresses.SingleAsync();
            Assert.False(saved.IsActive);
            Assert.Equal(1, saved.DaysCompleted);
            Assert.Equal(2, saved.ExercisesCompleted);
            Assert.Equal(2, await setup.DailyExerciseLogs.CountAsync());
            var stats = await Service(setup).GetWeeklyStatsAsync(user);
            Assert.Equal(2, stats.TotalExercises);
        }
        finally { await setup.Database.EnsureDeletedAsync(); }
    }
}
