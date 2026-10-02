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
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class GlobalScope : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken) => Task.FromResult(CoachingAdminScope.Global);
    }
}
