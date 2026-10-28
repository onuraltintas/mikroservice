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
    public async Task Full52OutcomePilotImportsInto13LocalTopics_AsReviewOnly_WithOfficial180PeriodBudget()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COACHING_PROFILE_TEST_POSTGRES")) { Pooling = false };
        Assert.Contains(connection.Host, new[] { "127.0.0.1", "localhost" });
        var database = "test_coaching_full_lgs_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            connection.Database = database;
            await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,
                x => x.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options);
            await db.Database.MigrateAsync();
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "data", "coaching-outcomes"))) root = root.Parent;
            Assert.NotNull(root);
            var package = System.Text.Json.JsonSerializer.Deserialize<OutcomeImportPackage>(File.ReadAllText(Path.Combine(root.FullName,
                "data", "coaching-outcomes", "lgs-matematik-2026-pilot.json")), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var lesson = StudyCatalogLesson.Create("approved-catalog", "00c641eb-c82d-4712-3b41-08dd3944e858", "Matematik", null, "LGS"); lesson.SetActive(true);
            var unit = StudyCatalogUnit.Create("approved-catalog", "f2805d63-5bec-4773-0062-08dd3b8d6103", lesson.Id, "Matematik (LGS)", 1); unit.SetActive(true);
            db.AddRange(lesson, unit);
            foreach (var map in package.Mappings)
            {
                var groupCode = package.Outcomes.Single(x => x.Code == map.OutcomeCodes[0]).OfficialTopicCode;
                var topic = StudyCatalogTopic.Create("approved-catalog", map.TopicSourceId, lesson.Id, unit.Id,
                    package.OfficialTopics.Single(x => x.Code == groupCode).Name, null, 1); topic.SetActive(true); db.Add(topic);
            }
            await db.SaveChangesAsync();
            await using var f = await CoachingOutcomePilotTests.Fixture.Create();
            var service = new CoachingOutcomeCatalogService(db, f.Scope, f.Inner.Actor);
            var preview = await service.PreviewAsync(package, default);
            Assert.Equal(13, preview.MatchedTopics); Assert.Empty(preview.UnmatchedTopics);
            await service.ImportAsync(new(package, preview.Fingerprint, "Tam LGS pilot aktarımı"), default);
            var edition = Assert.Single(await service.ListAsync(default));
            var detail = await service.GetAsync(edition.Id, default);
            Assert.Equal(52, detail.Outcomes.Count); Assert.Equal(52, detail.Estimates.Count);
            Assert.Equal(12, detail.OfficialTopics.Count); Assert.Equal(180, detail.OfficialTopics.Sum(x => x.LessonPeriods));
            Assert.Equal(58, detail.Mappings.Count); Assert.Equal(13, detail.Topics.Count);
            Assert.All(detail.Estimates, x => { Assert.False(x.IsPublished); Assert.Equal("OfficialBudgetDraft", x.Method); });
            Assert.All(detail.Mappings, x => Assert.False(x.IsApproved));
            Assert.Equal(2, await db.Set<StudyCatalogOutcomeSource>().CountAsync());
            await service.ImportAsync(new(package, (await service.PreviewAsync(package, default)).Fingerprint, "Tam paketi yeniden aktar"), default);
            Assert.Equal(52, await db.Set<StudyCatalogOutcome>().CountAsync());
        }
        finally
        { await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync(); }
    }

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
            await db.Database.MigrateAsync();
            var student = Guid.NewGuid();
            var oldPlan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Eski plan"); oldPlan.Activate();
            var oldWork = StudyPlanTask.Create(oldPlan, new(2026, 10, 19), "Tamamlanan çalışma", 30); oldWork.Complete(25, 8);
            var lesson = StudyCatalogLesson.Create("approved-catalog", "math-lgs", "Matematik", null, "LGS"); lesson.SetActive(true);
            var unit = StudyCatalogUnit.Create("approved-catalog", "math-unit", lesson.Id, "Matematik (LGS)", 1); unit.SetActive(true);
            var topic = StudyCatalogTopic.Create("approved-catalog", "topic:factors", lesson.Id, unit.Id, "Çarpanlar ve Katlar", null, 1); topic.SetActive(true);
            db.AddRange(oldPlan, oldWork, lesson, unit, topic); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            // This test used to restore the StudentLearningSetup schema and replay the outcome migrations over existing work.
            // That replay is no longer possible: UnifiedTargetCatalog (2026-10-20) cannot be reversed by design (restore the
            // pre-migration backup instead), and later migrations refuse to drop dated or partial work. The upgrade was verified
            // when those migrations were written; here the work survives re-running the migrations and the outcome import.
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
            db.ChangeTracker.Clear();
            await db.StudyPlanTasks.Where(x => x.RevisionId == plan.Id).ExecuteDeleteAsync();
            Assert.Empty(await db.Set<StudyPlanTaskOutcome>().ToArrayAsync());
            Assert.Equal(2, await db.Set<StudyCatalogOutcome>().CountAsync());
        }
        finally
        { await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
