using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingAdminCatalogPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task FiltersAndPaginationExecuteInPostgresWithoutWildcardExpansion()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            db.StudyCatalogLessons.AddRange(
                StudyCatalogLesson.Create("manual", "1", "100% Math", 8, "LGS"),
                StudyCatalogLesson.Create("manual", "2", "1000 Math", 8, "LGS"),
                StudyCatalogLesson.Create("other", "3", "Other Math", 8, "LGS"));
            await db.SaveChangesAsync();
            var reader = new CoachingAdminCatalogReader(db, new GlobalScope());
            var literal = await reader.ListAsync(CatalogKind.Lessons, new() { Search = "100%" }, default);
            Assert.Equal("100% Math", Assert.Single(literal.Items).Name);
            var page = await reader.ListAsync(CatalogKind.Lessons, new() { Source = "manual", PageSize = 1, PageNumber = 2 }, default);
            Assert.Equal(2, page.TotalCount);
            Assert.Single(page.Items);
            foreach (var kind in Enum.GetValues<CatalogKind>())
                await reader.ListAsync(kind, new(), default);
            var grade = await reader.ListAsync(CatalogKind.Lessons, new() { GradeNumber = 9 }, default);
            Assert.Empty(grade.Items);
            var exam = await reader.ListAsync(CatalogKind.Lessons, new() { ExamCode = "TYT" }, default);
            Assert.Empty(exam.Items);
            db.TargetUniversityPrograms.Add(TargetUniversityProgram.Create("manual", "u1", "Test University", "Engineering", "12345", "SAY", 400, 2025));
            var school = TargetSchool.Create("manual", "s1", "School", "Ankara", "Çankaya", 450, 2025);
            school.SetVerifiedLocation("06", "123");
            db.TargetSchools.Add(school);
            await db.SaveChangesAsync();
            Assert.Single((await reader.ListAsync(CatalogKind.UniversityPrograms, new() { Search = "Test University", ScoreType = "SAY", ScoreYear = 2025 }, default)).Items);
            Assert.Single((await reader.ListAsync(CatalogKind.UniversityPrograms, new() { Search = "12345" }, default)).Items);
            Assert.Empty((await reader.ListAsync(CatalogKind.UniversityPrograms, new() { ScoreType = "EA" }, default)).Items);
            Assert.Single((await reader.ListAsync(CatalogKind.Schools, new() { ProvinceId = "06", DistrictId = "123" }, default)).Items);
            Assert.Empty((await reader.ListAsync(CatalogKind.Schools, new() { ProvinceId = "34" }, default)).Items);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task ParentlessTopicsAreFilteredBeforeCountingAndPagination()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var lesson = StudyCatalogLesson.Create("manual", "l", "Lesson", 8, "LGS");
            var unit = StudyCatalogUnit.Create("manual", "u", lesson.Id, "Unit", 1);
            var parent = StudyCatalogTopic.Create("manual", "p", lesson.Id, unit.Id, "Z Parent", null, 1);
            var child = StudyCatalogTopic.Create("manual", "c", lesson.Id, unit.Id, "A Child", parent.Id, 1);
            db.AddRange(lesson, unit, parent, child);
            await db.SaveChangesAsync();
            var reader = new CoachingAdminCatalogReader(db, new GlobalScope());
            var page = await reader.ListAsync(CatalogKind.Topics, new() { HasParent = false, PageSize = 1 }, default);
            Assert.Equal(1, page.TotalCount);
            Assert.Equal(parent.Id, Assert.Single(page.Items).Id);
            Assert.Equal(child.Id, Assert.Single((await reader.ListAsync(CatalogKind.Topics, new() { HasParent = true }, default)).Items).Id);
            await Assert.ThrowsAsync<ArgumentException>(() => reader.ListAsync(CatalogKind.Lessons, new() { HasParent = false }, default));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class GlobalScope : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken) => Task.FromResult(CoachingAdminScope.Global);
    }
}
