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
            var loaded = await db.AcademicGoals.SingleAsync();
            Assert.Equal(500, loaded.TargetMaxScore);
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE coaching.academic_goals SET target_max_score = 100"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var previous = migrations[Array.FindIndex(migrations, x => x.EndsWith("_AddGoalScoreScale", StringComparison.Ordinal)) - 1];
            await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            loaded.SetScoreTarget(null, null, null); await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync(previous);
            await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.Equal(30, (await db.AcademicGoals.SingleAsync()).CurrentProgress);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
