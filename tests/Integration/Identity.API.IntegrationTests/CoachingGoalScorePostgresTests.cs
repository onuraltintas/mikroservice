using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingGoalScorePostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ScoreScaleMigration_EnforcesConsistencyAndDoesNotDiscardConfiguredTargetsOnRollback()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var goal = AcademicGoal.Create(Guid.NewGuid(), "Preserved", GoalCategory.ExamPreparation);
            goal.SetScoreTarget(400, 500, ExamType.LGS); goal.UpdateProgress(30);
            db.Add(goal); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(500, (await db.AcademicGoals.SingleAsync()).TargetMaxScore);
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE coaching.academic_goals SET target_max_score = 100"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);

            // The rollback guard is checked in the schema AddGoalScoreScale created: from the
            // latest schema, rolling back past UnifiedTargetCatalog requires a backup restore.
            await db.Database.EnsureDeletedAsync();
            var migrator = db.GetService<IMigrator>();
            var migrations = db.Database.GetMigrations().ToArray();
            var current = Array.FindIndex(migrations, x => x.EndsWith("_AddGoalScoreScale", StringComparison.Ordinal));
            await migrator.MigrateAsync(migrations[current]);
            var goalId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO coaching.academic_goals (id, student_id, title, category, current_progress, is_completed, created_at, "Version", target_score, target_max_score, target_exam_type)
                VALUES ({goalId}, {Guid.NewGuid()}, {"Preserved"}, {"ExamPreparation"}, {30}, {false}, {DateTime.UtcNow}, {0}, {400m}, {500m}, {"LGS"})
                """);
            await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(migrations[current - 1]));
            await db.Database.ExecuteSqlRawAsync("UPDATE coaching.academic_goals SET target_score = NULL, target_max_score = NULL, target_exam_type = NULL");
            await migrator.MigrateAsync(migrations[current - 1]);
            await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.Equal(30, (await db.AcademicGoals.SingleAsync(x => x.Id == goalId)).CurrentProgress);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
