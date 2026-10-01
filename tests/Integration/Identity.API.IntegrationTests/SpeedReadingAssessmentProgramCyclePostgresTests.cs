using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Persistence.Migrations;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingAssessmentProgramCyclePostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migration_preserves_history_and_program_cycles_are_isolated_in_postgres()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var age = AgeGroupConfiguration.Create(Guid.NewGuid(), "test", "Test", 13, 16,
                100, 200, 300, 70, 15, 1, 1, true, null, user, now);
            var template = ProgramTemplate.Import(Guid.NewGuid(), "Program", "", age.Id, 0, 100,
                "{}", 1, 2, 5, 1, 1, true, 1, 0, null, false, now, null, null, null);
            var first = CompletedProgram(user, template.Id, now.AddDays(-40), now.AddDays(-30));
            var latest = CompletedProgram(user, template.Id, now.AddDays(-20), now.AddDays(-1));
            db.AgeGroupConfigurations.Add(age);
            db.ProgramTemplates.Add(template);
            db.StudentProgramProgresses.AddRange(first, latest);
            var baseline = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.Baseline,
                "tr-baseline-v1", "tr", null, 3, now.AddDays(-50), null);
            baseline.Complete(now.AddDays(-50));
            var historicalPost = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.PostTraining,
                "tr-posttraining-v1", "tr", null, 3, now.AddDays(-29), null);
            historicalPost.Complete(now.AddDays(-29));
            db.AssessmentAttempts.AddRange(baseline, historicalPost);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var migration = new LinkAssessmentProgramCycles();
            var generator = db.GetService<IMigrationsSqlGenerator>();
            foreach (var command in generator.Generate(migration.DownOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            foreach (var command in generator.Generate(migration.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            Assert.Equal(2, await db.AssessmentAttempts.CountAsync());
            var historical = await db.AssessmentAttempts.SingleAsync(item => item.Id == historicalPost.Id);
            Assert.Null(historical.ProgramProgressId);
            historical.BindToProgram(first.Id);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!;
            var service = (ISpeedReadingAssessment)Activator.CreateInstance(type, db, null)!;
            var plan = await service.GetPhasePlanAsync(user, CancellationToken.None);
            Assert.Equal(AssessmentAttemptPhase.PostTraining, plan.NextPhase);
            Assert.Equal(AssessmentPhasePlanStatus.Available,
                plan.Phases.Single(item => item.Phase == AssessmentAttemptPhase.PostTraining).Status);
            var history = await service.GetAttemptHistoryAsync(user, CancellationToken.None);
            Assert.Equal(first.Id, history.Single(item => item.Id == historicalPost.Id).ProgramProgressId);
            Assert.Equal(2, history.Count);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static StudentProgramProgress CompletedProgram(Guid user, Guid template, DateTime assigned, DateTime completed) =>
        StudentProgramProgress.Import(Guid.NewGuid(), user, template, assigned,
            1, 1, 1, 1, 1, completed, false, completed, 80, 1, 1, assigned, null, null, null);
}
