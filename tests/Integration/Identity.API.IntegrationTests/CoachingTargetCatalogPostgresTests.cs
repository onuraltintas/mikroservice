using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingTargetCatalogPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TargetMigration_PreservesUnknownValuesRejectsDuplicatesAndRollsBack(bool university)
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, options => options.EnableRetryOnFailure()).Options);
        // This connection is owned by the disposable PostgreSQL fixture, never production.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA coaching; CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL);");
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261002082803_AddCoachingStudyCatalog";
            const string current = "20261002091400_AddCoachingTargetCatalog";
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(previous, current));
            // Current entity includes optional location IDs; preserve this historical migration test's schema compatibility.
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(
                "20261002134329_AddGoalScoreScale", "20261002151257_LinkTargetSchoolAdministrativeLocations"));
            db.TargetUniversityPrograms.Add(TargetUniversityProgram.Create("test", "program", "University", "Program", null, null, null));
            db.TargetSchools.Add(TargetSchool.Create("test", "school", "School", "City", "District", null));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var program = await db.TargetUniversityPrograms.SingleAsync();
            var school = await db.TargetSchools.SingleAsync();
            Assert.Null(program.MinimumScore);
            Assert.Null(program.ScoreYear);
            Assert.False(program.IsActive);
            Assert.Null(school.ScoreYear);
            Assert.False(school.IsActive);
            if (university)
                db.TargetUniversityPrograms.Add(TargetUniversityProgram.Create("test", "program", "Other", "Program", null, null, null));
            else
                db.TargetSchools.Add(TargetSchool.Create("test", "school", "Other", "City", "District", null));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.TargetUniversityPrograms.CountAsync());
            Assert.Equal(1, await db.TargetSchools.CountAsync());
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(current, previous));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.TargetSchools.CountAsync());
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.TargetUniversityPrograms.CountAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
