using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingStudyPlanPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Database_RestrictsActivePlansTaskOwnershipAndStaleUpdates()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options;
        await using var db = new CoachingDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var student = Guid.NewGuid();
            var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Plan");
            plan.Activate();
            var task = StudyPlanTask.Create(plan, new DateOnly(2026, 10, 2), "Review", 30);
            db.AddRange(plan, task);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var competing = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Other");
            competing.Activate();
            db.StudyPlanRevisions.Add(competing);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE coaching.study_plan_tasks SET \"StudentId\" = {Guid.NewGuid()} WHERE \"Id\" = {task.Id}"));
            await using var concurrentDb = new CoachingDbContext(options);
            var first = await db.StudyPlanTasks.SingleAsync();
            var stale = await concurrentDb.StudyPlanTasks.SingleAsync();
            first.Complete(20);
            await db.SaveChangesAsync();
            stale.Complete(25);
            await Assert.ThrowsAnyAsync<Exception>(() => concurrentDb.SaveChangesAsync());
            db.ChangeTracker.Clear();
            Assert.Equal(20, (await db.StudyPlanTasks.SingleAsync()).ActualMinutes);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
