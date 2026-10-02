using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingGoalCatalogPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task GoalCatalogLinks_RejectMissingTargetsAndPreserveReferencedHistory()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var school = TargetSchool.Create("test", "school", "School", "City", "District", null);
            var goal = AcademicGoal.Create(Guid.NewGuid(), "Target", GoalCategory.ExamPreparation);
            goal.SetCatalogTarget(null, school.Id);
            db.TargetSchools.Add(school);
            db.AcademicGoals.Add(goal);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            // Raw SQL exercises the database FK even when entities are not loaded.
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM coaching.target_schools WHERE \"Id\" = {school.Id}"));
            var stored = await db.AcademicGoals.SingleAsync();
            Assert.Equal(school.Id, stored.TargetSchoolId);
            stored.SetCatalogTarget(Guid.NewGuid(), null);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            Assert.Equal(school.Id, (await db.AcademicGoals.SingleAsync()).TargetSchoolId);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
