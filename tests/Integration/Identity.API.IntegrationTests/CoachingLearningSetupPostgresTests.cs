using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Identity.API.IntegrationTests;

public sealed class CoachingLearningSetupPostgresTests
{
    [CoachingProfilePostgresFact]
    public async Task UpgradeAndResumePreserveOldPlans_AndEnforceDatabaseRelationships()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COACHING_PROFILE_TEST_POSTGRES")) { Pooling = false };
        Assert.Contains(connection.Host, new[] { "127.0.0.1", "localhost" });
        var database = "test_coaching_setup_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            connection.Database = database;
            var options = new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,
                x => x.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options;
            await using var db = new CoachingDbContext(options);
            await db.GetService<IMigrator>().MigrateAsync("20261016134530_OwnedCoachingInvitations");
            var student = Guid.NewGuid(); var profile = CoachingStudentProfile.Create(student);
            profile.UpdateEducation(CoachingEducationStatus.Student, 12, null, null, null, null, null, null);
            var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Mevcut plan"); plan.Activate();
            var task = StudyPlanTask.Create(plan, new(2026, 10, 19), "Tamamlandı", 30); task.Complete(25);
            // The pre-upgrade catalog has no curriculum column yet.
            db.Add(profile); await db.SaveChangesAsync(); await CoachingLegacyPlanSeed.InsertAsync(db,plan,task); db.ChangeTracker.Clear();
            await db.Database.MigrateAsync();
            var curriculum = CurriculumVersion.Create("test-2026", "Test", "v1", 2026, 2026, "Test", null); curriculum.SetPublished(true);
            var lesson = StudyCatalogLesson.Create("test", "math", "Matematik", 8, "LGS"); lesson.AssignCurriculum(curriculum.Id); lesson.SetActive(true);
            var unit = StudyCatalogUnit.Create("test", "unit", lesson.Id, "Sayılar", 1); unit.SetActive(true);
            var topic = StudyCatalogTopic.Create("test", "topic", lesson.Id, unit.Id, "Sayılar", null, 1, 30); topic.SetActive(true);
            var hours = StudyAvailability.Create(student, "Europe/Istanbul"); hours.ReplaceWindows([new(DayOfWeek.Monday, 1020, 1080)]);
            db.AddRange(curriculum, lesson, unit, topic, hours); await db.SaveChangesAsync();
            var actor = new CoachingOwnedProfilePersistenceTests.Actor { UserId = student, Roles = ["Student"] };
            var accounts = new CoachingOwnedProfilePersistenceTests.FakeAccounts();
            accounts.Users[student] = new(student, "Test", "Student", "test@example.test", true, true, ["Student"], null);
            var service = new CoachingLearningSetupService(db, new CoachingAccessPolicy(actor), accounts);
            var saved = await service.SaveAsync(new(null, 2026, LearningPurpose.Lgs, 2027, null, null,
                [new(lesson.Id, 1, 3)], [new(topic.Id, DeclaredTopicStatus.NeedsReview)], true), default);
            Assert.True(saved.IsReady); db.ChangeTracker.Clear();
            var catalog = await service.GetCatalogAsync(lesson.Id, 1, default); Assert.Single(catalog.Topics.Items);
            await using var second = new CoachingDbContext(options);
            var resumedService = new CoachingLearningSetupService(second, new CoachingAccessPolicy(actor), accounts);
            var resumed = await resumedService.GetAsync(default); Assert.True(resumed.IsReady);
            var updated = await resumedService.SaveAsync(new(resumed.Context!.Version, 2026, LearningPurpose.Lgs, 2027, null, null,
                [new(lesson.Id, 2, null)], [new(topic.Id, DeclaredTopicStatus.Studied)], true), default);
            Assert.True(updated.Context!.Version > resumed.Context.Version);
            second.ChangeTracker.Clear();
            Assert.Equal(2, (await second.StudentCoursePreferences.SingleAsync()).Priority);
            var preview = await new CoachingAutomaticStudyPlanPreviewService(second, new CoachingAccessPolicy(actor), resumedService)
                .PreviewAsync(new(new(2026, 10, 19), 7, hours.Version, [new(topic.Id, 30)], updated.Context.Version));
            Assert.Equal(30, preview.Schedule.ScheduledMinutes);
            var preserved = await second.StudyPlanTasks.SingleAsync(); Assert.Equal(task.Id, preserved.Id); Assert.Equal(25, preserved.ActualMinutes);
            Assert.Equal(StudyPlanStatus.Active, (await second.StudyPlanRevisions.SingleAsync()).Status);
            Assert.Equal("StudentDeclaration", (await second.StudentTopicStates.SingleAsync()).Source);
            var deletion = new Coaching.Infrastructure.Catalogs.CoachingCatalogDeletionService(second, new GlobalScope(), actor);
            Assert.False((await deletion.GetUsageAsync(Coaching.Application.CatalogAdministration.CatalogKind.Topics, topic.Id, default)).CanDelete);
            second.StudentLearningContexts.Add(StudentLearningContext.Create(student, 2026, LearningPurpose.General, null, null, null));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicate.GetBaseException()).SqlState);
        }
        finally
        { await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync(); }
    }
    private sealed class GlobalScope : ICoachingAdminScopeAuthorization
    { public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(CoachingAdminScope.Global); }
}
