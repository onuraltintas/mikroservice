using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Domain.Assignments;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Persistence.Migrations;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingAssignmentMetricsPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Repair_clears_only_linked_unmeasured_assignment_metrics_and_is_repeatable()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "EyeTracking", "Takip", "motion_path");
            var exercise = Exercise.Create("Takip", "motion_path", "{}", 1, student, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
            var assignment = Assignment.Create(Guid.NewGuid(), exercise.Id, null, "Test", "", DateTime.UtcNow.AddDays(1));
            db.Assignments.Add(assignment);
            var rows = new List<StudentAssignment>();
            foreach (var measured in new[] { false, true })
            {
                var row = StudentAssignment.Assign(assignment.Id, Guid.NewGuid());
                var session = ExerciseSession.Start(row.StudentId, exercise.Id, null, 1, DateTime.UtcNow.AddMinutes(-1),
                    75, studentAssignmentId: row.Id);
                session.Complete(DateTime.UtcNow);
                var result = ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, row.StudentId, exercise.Id,
                    null, 0, 75, 0, 0, 0, 0, DateTime.UtcNow, "[]", "[]", false, measured);
                row.Complete(result.Id, 0, 0, DateTime.UtcNow);
                db.ExerciseSessions.Add(session); db.ExerciseSessionResults.Add(result); rows.Add(row);
            }
            var unmatched = StudentAssignment.Assign(assignment.Id, student);
            unmatched.Complete(Guid.NewGuid(), 65, 50, DateTime.UtcNow);
            db.StudentAssignments.AddRange(rows.Append(unmatched));
            await db.SaveChangesAsync();
            var persistedCompletionDate = await db.StudentAssignments.AsNoTracking()
                .Where(item => item.Id == rows[0].Id).Select(item => item.CompletionDate).SingleAsync();

            var migration = new ClearUnmeasuredAssignmentMetrics();
            var commands = db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model);
            foreach (var pass in new[] { 1, 2 })
                foreach (var command in commands)
                    await db.Database.ExecuteSqlRawAsync(command.CommandText);
            db.ChangeTracker.Clear();

            var unmeasured = await db.StudentAssignments.SingleAsync(item => item.Id == rows[0].Id);
            Assert.True(unmeasured.IsCompleted);
            Assert.Equal(rows[0].ResultId, unmeasured.ResultId);
            Assert.Equal(persistedCompletionDate, unmeasured.CompletionDate);
            Assert.Null(unmeasured.Score);
            Assert.Null(unmeasured.KeyPerformanceMetric);
            Assert.Equal(0m, (await db.StudentAssignments.SingleAsync(item => item.Id == rows[1].Id)).Score);
            Assert.Equal(65m, (await db.StudentAssignments.SingleAsync(item => item.Id == unmatched.Id)).Score);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
