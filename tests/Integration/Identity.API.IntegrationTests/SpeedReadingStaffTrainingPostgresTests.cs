using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Persistence.Migrations;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingStaffTrainingPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Staff_can_complete_two_days_immediately_but_normal_student_waits(bool staff)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var migration = new AddStaffTrainingMode();
            var generator = db.GetService<IMigrationsSqlGenerator>();
            foreach (var command in generator.Generate(migration.DownOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            foreach (var command in generator.Generate(migration.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            var user = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Fixation", "focus");
            db.ExerciseTypes.Add(type);
            var exercises = Enumerable.Range(0, 2).Select(_ => Exercise.Create("Exercise", "Fixation", "{}", 1, user, type.Id)).ToArray();
            db.Exercises.AddRange(exercises);
            var template = ProgramTemplate.Import(Guid.NewGuid(), "Two-day program", "", Guid.NewGuid(), 0, 100,
                "{\"week1\":{\"day1\":[{\"Type\":\"Fixation\",\"Count\":1,\"Difficulty\":1}],\"day2\":[{\"Type\":\"Fixation\",\"Count\":1,\"Difficulty\":1}]}}",
                1, 2, 5, 1, 2, true, 1, 0, null, false, now, null, null, null);
            db.ProgramTemplates.Add(template);
            var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now, staff);
            progress.SetSchedule(JsonSerializer.Serialize(exercises.Select((exercise, index) => new
            { weekNumber = 1, dayNumber = index + 1, order = 1, exerciseId = exercise.Id })), user, now);
            db.StudentProgramProgresses.Add(progress);
            var sessions = exercises.Select(exercise => ExerciseSession.Start(user, exercise.Id, null, 1, now, null)).ToArray();
            foreach (var session in sessions)
            {
                session.Complete(now.AddSeconds(1));
                db.ExerciseSessions.Add(session);
                db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, user,
                    session.ExerciseId, null, 0, 1, 0, 0, 0, 0, now, isMeasured: false));
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
            var daily = (ISpeedReadingDailyProgress)Activator.CreateInstance(assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDailyProgress")!, db, null)!;
            Task<CompleteDailyExerciseResponse> Complete(int index) => daily.CompleteExerciseAsync(user,
                new CompleteDailyExerciseRequest { ExerciseId = exercises[index].Id, SessionId = sessions[index].Id },
                sessions[index].Id.ToString(), CancellationToken.None);
            await Complete(0);
            if (!staff)
            {
                var error = await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => Complete(1));
                Assert.Equal("DailyProgress.DayLocked", error.Code);
                return;
            }
            var today = await daily.GetTodayExercisesAsync(user);
            Assert.Contains(today, item => item.ExerciseId == exercises[1].Id);
            var second = await Complete(1);
            Assert.True(second.ProgramCompleted);
            db.ChangeTracker.Clear();
            Assert.Equal(2, (await db.StudentProgramProgresses.SingleAsync()).DaysCompleted);
            var programs = (ISpeedReadingStudentProgram)Activator.CreateInstance(assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentProgram")!, db, null, null)!;
            var next = await programs.StartStaffTrainingAsync(user, template.Id, CancellationToken.None);
            Assert.NotEqual(progress.Id, next.ProgramId);
            Assert.Equal(2, await db.StudentProgramProgresses.CountAsync());
            Assert.Equal(1, await db.StudentProgramProgresses.CountAsync(item => item.IsActive));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
