using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingStudyCatalogPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("duplicate-source")]
    [InlineData("wrong-lesson")]
    [InlineData("wrong-parent-unit")]
    public async Task CatalogMigration_RejectsInvalidRelationshipsAndRollsBack(string invalidCase)
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, options => options.EnableRetryOnFailure()).Options);
        // Only the disposable fixture database is modified by this test.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA coaching; CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL);");
            var migrator = db.GetService<IMigrator>();
            const string previous = "20260930193749_AddCoachingBankTransferRequestReferenceIndex";
            const string current = "20261002082803_AddCoachingStudyCatalog";
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(previous, current));
            // Rows are written with SQL in this migration's schema; today's model maps
            // columns added later (curriculum versions, outcomes).
            Guid first = Guid.NewGuid(), second = Guid.NewGuid(), unit = Guid.NewGuid(), otherUnit = Guid.NewGuid(), parent = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO coaching.study_catalog_lessons ("Id", "Source", "SourceId", "Name", "GradeNumber", "IsActive")
                VALUES ({first}, 'test', 'first', 'First', 8, true), ({second}, 'test', 'second', 'Second', 8, true);
                INSERT INTO coaching.study_catalog_units ("Id", "Source", "SourceId", "LessonId", "Name", "IsActive")
                VALUES ({unit}, 'test', 'unit', {first}, 'Unit', true), ({otherUnit}, 'test', 'other-unit', {first}, 'Other unit', true);
                INSERT INTO coaching.study_catalog_topics ("Id", "Source", "SourceId", "LessonId", "UnitId", "Name", "IsActive")
                VALUES ({parent}, 'test', 'parent', {first}, {unit}, 'Parent', false);
                """);

            var invalid = invalidCase switch
            {
                "duplicate-source" => db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO coaching.study_catalog_lessons ("Id", "Source", "SourceId", "Name", "GradeNumber", "IsActive")
                    VALUES ({Guid.NewGuid()}, 'test', 'first', 'Duplicate', 8, true)
                    """),
                "wrong-lesson" => db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO coaching.study_catalog_topics ("Id", "Source", "SourceId", "LessonId", "UnitId", "Name", "DisplayOrder", "IsActive")
                    VALUES ({Guid.NewGuid()}, 'test', 'wrong', {second}, {unit}, 'Wrong', 1, true)
                    """),
                "wrong-parent-unit" => db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO coaching.study_catalog_topics ("Id", "Source", "SourceId", "LessonId", "UnitId", "ParentId", "Name", "DisplayOrder", "IsActive")
                    VALUES ({Guid.NewGuid()}, 'test', 'wrong', {first}, {otherUnit}, {parent}, 'Wrong', 1, true)
                    """),
                _ => throw new ArgumentOutOfRangeException(nameof(invalidCase))
            };
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => invalid);
            Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM coaching.study_catalog_lessons").SingleAsync());
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM coaching.study_catalog_topics").SingleAsync());
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(current, previous));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
                db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM coaching.study_catalog_lessons").SingleAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
