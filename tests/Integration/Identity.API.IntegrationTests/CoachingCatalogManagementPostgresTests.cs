using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingCatalogManagementPostgresTests(PostgresFixture postgres)
{
    private CoachingDbContext Database() => new(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
    private static CoachingCatalogManagementService Service(CoachingDbContext db, bool global = true)
        => new(db, new Scope(global), new TestUser(), new Locations());

    [Theory]
    [InlineData("400.12345")]
    [InlineData("1000000")]
    public async Task RejectsScoresNotRepresentableInDatabaseBeforeSaving(string value)
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var score = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            await Assert.ThrowsAsync<ArgumentException>(() => Service(db).CreateAsync(CatalogKind.UniversityPrograms,
                new("Program", "Test kaydı", UniversityName: "University", MinimumScore: score), default));
            Assert.False(await db.AdminAuditRecords.AnyAsync());
            Assert.False(await db.TargetUniversityPrograms.AnyAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData(CatalogKind.Lessons)]
    [InlineData(CatalogKind.Units)]
    [InlineData(CatalogKind.Topics)]
    [InlineData(CatalogKind.Schools)]
    [InlineData(CatalogKind.UniversityPrograms)]
    public async Task CreatesInactiveThenEditsAndPublishesWithAudit(CatalogKind kind)
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var lesson = StudyCatalogLesson.Create("fixture", "l", "Lesson", 8, "LGS");
            lesson.SetActive(true);
            var unit = StudyCatalogUnit.Create("fixture", "u", lesson.Id, "Unit", 1);
            unit.SetActive(true);
            db.AddRange(lesson, unit);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = Service(db);
            var request = kind switch
            {
                CatalogKind.Lessons => new CatalogSaveRequest("Matematik", "Yeni kayıt", GradeNumber: 8, ExamCode: "LGS"),
                CatalogKind.Units => new CatalogSaveRequest("Ünite", "Yeni kayıt", LessonId: lesson.Id, DisplayOrder: 1),
                CatalogKind.Topics => new CatalogSaveRequest("Konu", "Yeni kayıt", LessonId: lesson.Id, UnitId: unit.Id, EstimatedMinutes: 30),
                CatalogKind.Schools => new CatalogSaveRequest("Okul", "Yeni kayıt", ProvinceId: "66", DistrictId: "1", MinimumScore: 400, ScoreYear: 2026),
                _ => new CatalogSaveRequest("Program", "Yeni kayıt", UniversityName: "Üniversite", MinimumScore: 400, ScoreYear: 2026)
            };
            var created = await service.CreateAsync(kind, request, default);
            Assert.False(created.Data.GetProperty("IsActive").GetBoolean());
            Assert.Equal("admin-manual", created.Data.GetProperty("Source").GetString());
            var id = created.Data.GetProperty("Id").GetGuid();
            var edited = await service.UpdateAsync(kind, id, request with { Name = "Düzenlendi", Fingerprint = created.Fingerprint }, default);
            Assert.Equal("Düzenlendi", edited.Data.GetProperty("Name").GetString());
            Assert.Equal(created.Data.GetProperty("SourceId").GetString(), edited.Data.GetProperty("SourceId").GetString());
            await service.SetActiveAsync(kind, id, new(edited.Fingerprint, true, "Yayın onayı"), default);
            Assert.True((await service.GetAsync(kind, id, default)).Data.GetProperty("IsActive").GetBoolean());
            Assert.Equal(3, await db.AdminAuditRecords.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task RejectsStaleWritesBadOwnershipAndInstitutionScopeWithoutChangingRecords()
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var service = Service(db);
            var created = await service.CreateAsync(CatalogKind.Lessons, new("Math", "Test kaydı", GradeNumber: 8), default);
            var id = created.Data.GetProperty("Id").GetGuid();
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(CatalogKind.Lessons, id, new("Other", "Test kaydı", Fingerprint: new string('0', 64)), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(CatalogKind.Units, new("Unit", "Test kaydı", LessonId: Guid.NewGuid()), default));
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, false).GetAsync(CatalogKind.Lessons, id, default));
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, false).CreateAsync(CatalogKind.Lessons, new("Other", "Test kaydı"), default));
            Assert.Equal("Math", (await service.GetAsync(CatalogKind.Lessons, id, default)).Data.GetProperty("Name").GetString());
            Assert.Equal(1, await db.AdminAuditRecords.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task VerifiesSchoolLocationAndRejectsIncompatibleFieldsAndInvalidReason()
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var service = Service(db);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(CatalogKind.Schools, new("School", "Test kaydı", ProvinceId: "66", DistrictId: "wrong"), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(CatalogKind.Lessons, new("Math", "Test kaydı", UniversityName: "Wrong kind"), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(CatalogKind.Lessons, new("Math", ""), default));
            var school = await service.CreateAsync(CatalogKind.Schools, new("School", "Test kaydı", ProvinceId: "66", DistrictId: "1"), default);
            Assert.Equal("Yozgat", school.Data.GetProperty("City").GetString());
            Assert.Equal("1", school.Data.GetProperty("DistrictId").GetString());
            Assert.Equal(1, await db.AdminAuditRecords.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task RejectsPublishingUnderInactiveParentAndDeactivatingParentWithActiveChildren()
    {
        await using var db = Database();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var service = Service(db);
            var lesson = await service.CreateAsync(CatalogKind.Lessons, new("Math", "Test kaydı"), default);
            var lessonId = lesson.Data.GetProperty("Id").GetGuid();
            var unit = await service.CreateAsync(CatalogKind.Units, new("Unit", "Test kaydı", LessonId: lessonId), default);
            var unitId = unit.Data.GetProperty("Id").GetGuid();
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetActiveAsync(CatalogKind.Units, unitId, new(unit.Fingerprint, true, "Yayın onayı"), default));
            await service.SetActiveAsync(CatalogKind.Lessons, lessonId, new(lesson.Fingerprint, true, "Yayın onayı"), default);
            await service.SetActiveAsync(CatalogKind.Units, unitId, new(unit.Fingerprint, true, "Yayın onayı"), default);
            lesson = await service.GetAsync(CatalogKind.Lessons, lessonId, default);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetActiveAsync(CatalogKind.Lessons, lessonId, new(lesson.Fingerprint, false, "Pasif yapma"), default));
            Assert.True((await service.GetAsync(CatalogKind.Lessons, lessonId, default)).Data.GetProperty("IsActive").GetBoolean());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Scope(bool global) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CoachingAdminScope(global, global ? null : Guid.NewGuid()));
    }
    private sealed class Locations : ICoachingLocationDirectory
    {
        public Task<IReadOnlyList<LocationProvince>> GetProvincesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<LocationProvince>>([new("66", "Yozgat")]);
        public Task<IReadOnlyList<LocationDistrict>> GetDistrictsAsync(string provinceId, CancellationToken ct) => Task.FromResult<IReadOnlyList<LocationDistrict>>([new("1", "66", "Merkez")]);
        public Task<bool> VerifyPairAsync(string provinceId, string districtId, CancellationToken ct) => Task.FromResult(provinceId == "66" && districtId == "1");
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
