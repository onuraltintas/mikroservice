using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Identity.API.IntegrationTests;

public sealed class CoachingProfilePostgresFactAttribute : FactAttribute
{
    public CoachingProfilePostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COACHING_PROFILE_TEST_POSTGRES")))
            Skip = "Set COACHING_PROFILE_TEST_POSTGRES to a disposable local PostgreSQL instance.";
    }
}

public sealed class CoachingOwnedProfilePostgresTests
{
    [CoachingProfilePostgresFact]
    public async Task UpgradePreservesCompletedWorkAndEnforcesUserIdRelationships()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COACHING_PROFILE_TEST_POSTGRES"));
        Assert.Contains(connection.Host, new[] { "127.0.0.1", "localhost" });
        connection.Pooling = false;
        var testName = "test_coaching_ownership_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{testName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            connection.Database = testName;
            var options = new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,
                x => x.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options;
            await using var db = new CoachingDbContext(options);
            var baseline = db.Database.GetMigrations().Last(x => !x.Contains("OwnedCoachingProfiles"));
            await db.GetService<IMigrator>().MigrateAsync(baseline);
            var student = Guid.NewGuid(); var teacher = Guid.NewGuid();
            var revision = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Existing work"); revision.Activate();
            var task = StudyPlanTask.Create(revision, new(2026, 10, 16), "Completed", 30, plannedQuestions: 10);
            task.Complete(25, 8); db.AddRange(revision, task); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.MigrateAsync();
            db.AddRange(CoachingStudentProfile.Create(student), CoachingTeacherProfile.Create(teacher));
            await db.SaveChangesAsync();
            db.Add(CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var preserved = await db.StudyPlanTasks.SingleAsync();
            Assert.Equal(task.Id, preserved.Id); Assert.Equal(student, preserved.StudentId);
            Assert.Equal(25, preserved.ActualMinutes); Assert.Equal(8, preserved.ActualQuestions); Assert.True(preserved.IsCompleted);
            Assert.Equal(student, (await db.CoachingTeacherStudentAssignments.SingleAsync()).StudentUserId);
            db.Add(CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicate.GetBaseException()).SqlState);
            db.ChangeTracker.Clear();
            db.Add(CoachingTeacherStudentAssignment.Create(teacher, Guid.NewGuid(), null, null, teacher));
            var missingUser = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)missingUser.GetBaseException()).SqlState);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{testName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
