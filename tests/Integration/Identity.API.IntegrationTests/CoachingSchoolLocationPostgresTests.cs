using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingSchoolLocationPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task LocationMigrationPreservesLegacyNamesAndEnforcesPairedIds()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA coaching; CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL);");
            var migrator = db.GetService<IMigrator>();
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(
                "20261002082803_AddCoachingStudyCatalog", "20261002091400_AddCoachingTargetCatalog"));
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO coaching.target_schools ("Id", "Source", "SourceId", "Name", "City", "District", "IsActive")
                VALUES ('10000000-0000-0000-0000-000000000001', 'test', 'school', 'School', 'ANKARA', 'ÇANKAYA (MERKEZ)', false);
                """);
            const string previous = "20261002134329_AddGoalScoreScale";
            const string current = "20261002151257_LinkTargetSchoolAdministrativeLocations";
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(previous, current));
            // Read and written with SQL: today's model maps schools to the later unified target_catalog.
            Task<SchoolLocation> SchoolAsync() => db.Database.SqlQueryRaw<SchoolLocation>(
                "SELECT \"ProvinceId\", \"DistrictId\", \"District\" FROM coaching.target_schools").SingleAsync();
            Assert.Equal(new SchoolLocation(null, null, "ÇANKAYA (MERKEZ)"), await SchoolAsync());
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE coaching.target_schools SET \"ProvinceId\" = 'TUR006'"));
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE coaching.target_schools SET \"ProvinceId\" = 'TUR006', \"DistrictId\" = 'TUR006007'");
            Assert.Equal("TUR006007", (await SchoolAsync()).DistrictId);
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(current, previous)));
            await db.Database.ExecuteSqlRawAsync("UPDATE coaching.target_schools SET \"ProvinceId\" = NULL, \"DistrictId\" = NULL");
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(current, previous));
            var district = await db.Database.SqlQueryRaw<string>("SELECT \"District\" AS \"Value\" FROM coaching.target_schools").SingleAsync();
            Assert.Equal("ÇANKAYA (MERKEZ)", district);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed record SchoolLocation(string? ProvinceId, string? DistrictId, string? District);
}
