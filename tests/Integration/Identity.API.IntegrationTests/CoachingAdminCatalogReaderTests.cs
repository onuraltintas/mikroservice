using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminCatalogReaderTests
{
    private static CoachingDbContext Database() => new(new DbContextOptionsBuilder<CoachingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task InstitutionScopeCannotReadSharedCatalogAdministration()
    {
        using var db = Database();
        var reader = new CoachingAdminCatalogReader(db, new Scope(new(false, Guid.NewGuid())));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => reader.ListAsync(
            CatalogKind.Schools, new(), default));
        Assert.Equal("Authorization.Forbidden", error.Code);
    }

    [Fact]
    public async Task ListsInactiveRecordsAndKeepsUnverifiedSourceLocationVisible()
    {
        using var db = Database();
        db.TargetSchools.Add(TargetSchool.Create("source", "1", "School", "ANKARA", "ÇANKAYA (MERKEZ)", null));
        await db.SaveChangesAsync();
        var page = await new CoachingAdminCatalogReader(db, new Scope(CoachingAdminScope.Global))
            .ListAsync(CatalogKind.Schools, new(), default);
        Assert.Equal(1, page.TotalCount);
        Assert.False(page.Items.Single().IsActive);
        Assert.Equal("ÇANKAYA (MERKEZ)", page.Items.Single().District);
        Assert.Null(page.Items.Single().DistrictId);
    }

    [Fact]
    public async Task ActiveFilterDoesNotSilentlyExposeInactiveRecords()
    {
        using var db = Database();
        db.StudyCatalogLessons.Add(StudyCatalogLesson.Create("source", "1", "Math", 8, "LGS"));
        await db.SaveChangesAsync();
        var page = await new CoachingAdminCatalogReader(db, new Scope(CoachingAdminScope.Global))
            .ListAsync(CatalogKind.Lessons, new() { IsActive = true }, default);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task InvalidPaginationIsRejected(int page, int size)
    {
        using var db = Database();
        await Assert.ThrowsAsync<ArgumentException>(() => new CoachingAdminCatalogReader(db, new Scope(CoachingAdminScope.Global))
            .ListAsync(CatalogKind.Topics, new() { PageNumber = page, PageSize = size }, default));
    }

    [Fact]
    public async Task FiltersForOtherCatalogKindsAndInvalidBoundsAreRejected()
    {
        using var db = Database();
        var reader = new CoachingAdminCatalogReader(db, new Scope(CoachingAdminScope.Global));
        foreach (var filter in new AdminCatalogFilter[]
        {
            new() { GradeNumber = 13 }, new() { LessonId = Guid.Empty },
            new() { ProvinceId = "06" }, new() { ScoreYear = 2025 }, new() { UnitId = Guid.NewGuid() }
        })
            await Assert.ThrowsAsync<ArgumentException>(() => reader.ListAsync(CatalogKind.Lessons, filter, default));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ListAsync(CatalogKind.Schools, new() { DistrictId = "123" }, default));
    }

    private sealed class Scope(CoachingAdminScope scope) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken) => Task.FromResult(scope);
    }
}
