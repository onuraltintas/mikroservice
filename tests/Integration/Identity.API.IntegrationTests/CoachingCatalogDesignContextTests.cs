using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCatalogDesignContextTests
{
    [Fact]
    public void DesignContextCannotTargetProductionAndDoesNotConnect()
    {
        using var db = new CoachingDesignTimeDbContextFactory().CreateDbContext([]);
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        Assert.Equal("127.0.0.1", connection.Host);
        Assert.Equal(1, connection.Port);
        Assert.Equal("coaching_design_only", connection.Database);
        Assert.NotNull(db.Model.FindEntityType(typeof(Coaching.Domain.Entities.TargetSchool)));
    }
}
