using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Identity.API.IntegrationTests;

public sealed class CoachingOutcomePostgresTests
{
    [CoachingProfilePostgresFact]
    public async Task MigrationPreservesWork_ImportAndSnapshotPersist_AndPersonalExportIncludesOnlyOwnAllocations()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COACHING_PROFILE_TEST_POSTGRES")) { Pooling = false };
        Assert.Contains(connection.Host, new[] { "127.0.0.1", "localhost" });
        var database = "test_coaching_outcomes_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            connection.Database = database;
            var options = new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,
                x => x.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options;
            await using var db = new CoachingDbContext(options);
            await db.GetService<IMigrator>().MigrateAsync("20261016144923_StudentLearningSetup");
            var student = Guid.NewGuid();
            var oldPlan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Eski plan"); oldPlan.Activate();
            var oldWork = StudyPlanTask.Create(oldPlan, new(2026, 10, 19), "Tamamlanan çalışma", 30); oldWork.Complete(25, 8);
            var lesson = StudyCatalogLesson.Create("approved-catalog", "math-lgs", "Matematik", null, "LGS"); lesson.SetActive(true);
            var unit = StudyCatalogUnit.Create("approved-catalog", "math-unit", lesson.Id, "Matematik (LGS)", 1); unit.SetActive(true);
            var topic = StudyCatalogTopic.Create("approved-catalog", "topic:factors", lesson.Id, unit.Id, "Çarpanlar ve Katlar", null, 1); topic.SetActive(true);
            db.AddRange(oldPlan, oldWork, lesson, unit, topic); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            await using var f = await CoachingOutcomePilotTests.Fixture.Create();
            var service = new CoachingOutcomeCatalogService(db, f.Scope, f.Inner.Actor);
            var review = await service.PreviewAsync(f.Package, default);
            await service.ImportAsync(new(f.Package, review.Fingerprint, "İlk pilot aktarımı"), default);
            await service.ImportAsync(new(f.Package, (await service.PreviewAsync(f.Package, default)).Fingerprint, "Tekrarlı aktarım"), default);
            Assert.Equal(2, await db.Set<StudyCatalogOutcome>().CountAsync());
            var usage = await new CoachingCatalogDeletionService(db, f.Scope, f.Inner.Actor)
                .GetUsageAsync(Coaching.Application.CatalogAdministration.CatalogKind.Topics, topic.Id, default);
            Assert.False(usage.CanDelete);
            var outcome = await db.Set<StudyCatalogOutcome>().OrderBy(x => x.Code).FirstAsync();
            await service.ReviewMappingAsync(topic.Id, outcome.Id, new(true, "Konu kapsamı incelendi"), default);
            var estimate = await service.EstimateAsync(outcome.Id, new(20, 30, 10, "Öğretmen tarafından pilot süre incelemesi", "Medium", "Süre incelemesi"), default);
            await service.PublishAsync(estimate.Id, "Süre onaylanarak yayınlandı", default);
            f.Inner.Actor.UserId = student; f.Inner.Actor.Roles = ["Student"];
            var hours = StudyAvailability.Create(student, "Europe/Istanbul"); hours.ReplaceWindows([new(DayOfWeek.Monday, 1020, 1140)]);
            db.Add(hours); await db.SaveChangesAsync();
            var access = new CoachingAccessPolicy(f.Inner.Actor);
            var previews = new CoachingAutomaticStudyPlanPreviewService(db, access);
            var plans = new CoachingManualStudyPlanService(db, access, previews, new NoopCoachingEventPublisher());
            var r = new AutomaticStudyPreviewRequest(new(2026, 10, 19), 7, hours.Version, [new(topic.Id, null, [new(outcome.Id, estimate.Id)])]);
            var preview = await previews.PreviewAsync(r);
            var plan = await plans.CreateAutomaticDraftAsync(new("Kazanım pilotu", r, preview.ActiveRevisionId, preview.ActiveRevisionVersion));
            Assert.Equal(60, Assert.Single(await db.Set<StudyPlanTaskOutcome>().ToArrayAsync()).AllocatedMinutes);
            Assert.Equal(estimate.Id, Assert.Single(plan.Tasks.Single().Outcomes!).EstimateId);
            var export = await new CoachingDataExportRepository(db).ExportStudentDataAsync(student, DateTimeOffset.UtcNow, default);
            Assert.Contains(export.StudyPlans!, x => x.Tasks.Any(t => t.Outcomes!.Any(o => o.EstimateId == estimate.Id)));
            var preserved = await db.StudyPlanTasks.SingleAsync(x => x.Id == oldWork.Id);
            Assert.Equal(25, preserved.ActualMinutes); Assert.Equal(8, preserved.ActualQuestions);
            Assert.Equal(StudyPlanStatus.Active, (await db.StudyPlanRevisions.SingleAsync(x => x.Id == oldPlan.Id)).Status);
            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                db.Add(StudyPlanTaskOutcome.Create(plan.Tasks[0].Id, Guid.NewGuid(), estimate.Id, 5, "Catalog")); await db.SaveChangesAsync();
            });
        }
        finally
        { await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
