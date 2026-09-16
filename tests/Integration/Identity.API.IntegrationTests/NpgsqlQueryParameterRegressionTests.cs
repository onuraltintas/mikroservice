using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class NpgsqlQueryParameterRegressionTests
{
    [Fact]
    public void GuidArrayFiltersUseRelationalArrayParameterWithoutSpanEvaluation()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=not-used;Username=test;Password=test")
            .Options;
        using var context = new IdentityDbContext(options);
        var userIds = new[] { Guid.NewGuid() };

        var query = context.StudentProfiles
            .Where(profile => Enumerable.Contains(userIds, profile.UserId));

        var sql = query.ToQueryString();

        sql.Should().Contain("ANY");
        sql.Should().NotContain("ReadOnlySpan");
    }
}
