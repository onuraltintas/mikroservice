using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EduPlatform.Shared.Infrastructure.Middleware;
using System.Security.Claims;
using System.Data.Common;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingCatalogDeletionPostgresTests(PostgresFixture postgres)
{
    private CoachingDbContext Database() => new(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
    private static CoachingCatalogDeletionService Service(CoachingDbContext db, bool global = true)
    {
        return new(db, new Scope(global), new TestUser());
    }

    [Fact]
    public async Task UnusedSchoolIsPermanentlyRemovedAndAuditedInTheSameTransaction()
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var school = TargetSchool.Create("admin", "s1", "School", "City", "District", null);
            db.TargetSchools.Add(school);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = Service(db);
            var usage = await service.GetUsageAsync(CatalogKind.Schools, school.Id, default);
            Assert.True(usage.CanDelete);
            await service.DeleteAsync(CatalogKind.Schools, school.Id, new(usage.Fingerprint, "Unused duplicate", school.Id), default);
            Assert.False(await db.TargetSchools.AnyAsync());
            var audit = await db.AdminAuditRecords.SingleAsync();
            Assert.Equal("CatalogPermanentDelete", audit.Action);
            Assert.Contains("Unused duplicate", audit.ChangedFieldsJson);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task ReferencedLessonCannotBeDeletedAndInvalidConfirmationCannotDeleteAnything()
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var lesson = StudyCatalogLesson.Create("admin", "l1", "Math", 8, "LGS");
            db.StudyCatalogLessons.Add(lesson);
            db.StudyCatalogUnits.Add(StudyCatalogUnit.Create("admin", "u1", lesson.Id, "Unit", 1));
            await db.SaveChangesAsync();
            var service = Service(db);
            var usage = await service.GetUsageAsync(CatalogKind.Lessons, lesson.Id, default);
            Assert.False(usage.CanDelete);
            Assert.Equal(1, usage.CatalogReferences);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(CatalogKind.Lessons, lesson.Id, new(usage.Fingerprint, "Duplicate", lesson.Id), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync(CatalogKind.Lessons, lesson.Id, new(usage.Fingerprint, "Duplicate", Guid.NewGuid()), default));
            Assert.True(await db.StudyCatalogLessons.AnyAsync());
            Assert.False(await db.AdminAuditRecords.AnyAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task StaleFingerprintAndInstitutionScopeCannotDelete()
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var program = TargetUniversityProgram.Create("admin", "u1", "University", "Program", null, null, null);
            db.TargetUniversityPrograms.Add(program);
            await db.SaveChangesAsync();
            var service = Service(db);
            var usage = await service.GetUsageAsync(CatalogKind.UniversityPrograms, program.Id, default);
            program.Edit("University", "Changed", null, null, null, null);
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(CatalogKind.UniversityPrograms, program.Id, new(usage.Fingerprint, "Duplicate", program.Id), default));
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, false).GetUsageAsync(CatalogKind.UniversityPrograms, program.Id, default));
            Assert.True(await db.TargetUniversityPrograms.AnyAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task ExamJsonAndGoalReferencesBlockDeletionEvenWithoutCatalogForeignKeys()
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var lesson = StudyCatalogLesson.Create("admin", "l1", "Math", 8, "LGS");
            var school = TargetSchool.Create("admin", "s1", "School", "City", "District", null);
            db.AddRange(lesson, school);
            var exam = Exam.Create(Guid.NewGuid(), "Exam", ExamType.LGS, DateTime.UtcNow, 500);
            db.Exams.Add(exam);
            var result = ExamResult.Create(exam.Id, Guid.NewGuid(), 400);
            result.SetAnswerStatistics(1, 0, 0);
            result.SetLessonAnswers([new(lesson.Id, null, 1, 1, 0, 0)]);
            db.ExamResults.Add(result);
            var goal = AcademicGoal.Create(Guid.NewGuid(), "School target", GoalCategory.ExamPreparation);
            goal.SetCatalogTarget(null, school.Id);
            db.AcademicGoals.Add(goal);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = Service(db);
            var lessonUsage = await service.GetUsageAsync(CatalogKind.Lessons, lesson.Id, default);
            var schoolUsage = await service.GetUsageAsync(CatalogKind.Schools, school.Id, default);
            Assert.Equal(1, lessonUsage.ExamReferences);
            Assert.Equal(1, schoolUsage.GoalReferences);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(CatalogKind.Lessons, lesson.Id, new(lessonUsage.Fingerprint, "Duplicate", lesson.Id), default));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(CatalogKind.Schools, school.Id, new(schoolUsage.Fingerprint, "Duplicate", school.Id), default));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData(CatalogKind.Lessons)]
    [InlineData(CatalogKind.Units)]
    [InlineData(CatalogKind.Topics)]
    [InlineData(CatalogKind.UniversityPrograms)]
    public async Task EveryUnusedCatalogKindCanBePermanentlyDeleted(CatalogKind kind)
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var lesson = StudyCatalogLesson.Create("admin", "l1", "Math", 8, "LGS");
            var unit = StudyCatalogUnit.Create("admin", "u1", lesson.Id, "Unit", 1);
            object entity = kind switch
            {
                CatalogKind.Lessons => lesson,
                CatalogKind.Units => unit,
                CatalogKind.Topics => StudyCatalogTopic.Create("admin", "t1", lesson.Id, unit.Id, "Topic", null, 1, 30),
                _ => TargetUniversityProgram.Create("admin", "p1", "University", "Program", null, null, null)
            };
            if (kind is CatalogKind.Units or CatalogKind.Topics) db.Add(lesson);
            if (kind == CatalogKind.Topics) db.Add(unit);
            db.Add(entity);
            await db.SaveChangesAsync();
            var id = (Guid)db.Entry(entity).Property("Id").CurrentValue!;
            db.ChangeTracker.Clear();
            var service = Service(db);
            var usage = await service.GetUsageAsync(kind, id, default);
            Assert.True(usage.CanDelete);
            await service.DeleteAsync(kind, id, new(usage.Fingerprint, "Unused duplicate", id), default);
            Assert.Single(await db.AdminAuditRecords.ToListAsync());
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetUsageAsync(kind, id, default));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task TransientSaveFailureRetriesWithoutDuplicateTrackedEntitiesOrAuditRecords()
    {
        var interceptor = new FailFirstAuditSave();
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, options => options.EnableRetryOnFailure(2, TimeSpan.Zero, null))
            .AddInterceptors(interceptor).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var school = TargetSchool.Create("admin", "s1", "School", "City", "District", null);
            db.TargetSchools.Add(school);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = Service(db);
            var usage = await service.GetUsageAsync(CatalogKind.Schools, school.Id, default);
            await service.DeleteAsync(CatalogKind.Schools, school.Id, new(usage.Fingerprint, "Unused duplicate", school.Id), default);
            Assert.True(interceptor.Failed);
            Assert.False(await db.TargetSchools.AnyAsync());
            Assert.Single(await db.AdminAuditRecords.ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task LostCommitAcknowledgementReturnsSuccessWithOneDeletionAndOneAudit()
    {
        var interceptor = new LoseCommitAcknowledgement();
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, options => options.EnableRetryOnFailure(2, TimeSpan.Zero, null))
            .AddInterceptors(interceptor).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var school = TargetSchool.Create("admin", "s1", "School", "City", "District", null);
            db.TargetSchools.Add(school);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = Service(db);
            var usage = await service.GetUsageAsync(CatalogKind.Schools, school.Id, default);
            interceptor.Armed = true;
            await service.DeleteAsync(CatalogKind.Schools, school.Id, new(usage.Fingerprint, "Unused duplicate", school.Id), default);
            Assert.True(interceptor.Failed);
            Assert.False(await db.TargetSchools.AnyAsync());
            Assert.Single(await db.AdminAuditRecords.ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class LoseCommitAcknowledgement : DbTransactionInterceptor
    {
        public bool Armed { get; set; }
        public bool Failed { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Armed && !Failed)
            {
                Failed = true;
                throw new TimeoutException("Simulated lost commit acknowledgement.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FailFirstAuditSave : SaveChangesInterceptor
    {
        public bool Failed { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Failed && eventData.Context!.ChangeTracker.Entries<AdminAuditRecord>().Any(x => x.State == EntityState.Added))
            {
                Failed = true;
                throw new TimeoutException("Simulated transient audit save failure.");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Scope(bool global) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CoachingAdminScope(global, global ? null : Guid.NewGuid()));
    }

    private sealed class TestUser : ICurrentUserService
    {
        public Guid? UserId => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => ["SystemAdmin"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
