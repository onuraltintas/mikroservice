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
            var first = StudyCatalogLesson.Create("test", "first", "First", 8, null);
            var second = StudyCatalogLesson.Create("test", "second", "Second", 8, null);
            var unit = StudyCatalogUnit.Create("test", "unit", first.Id, "Unit", null);
            var otherUnit = StudyCatalogUnit.Create("test", "other-unit", first.Id, "Other unit", null);
            var parent = StudyCatalogTopic.Create("test", "parent", first.Id, unit.Id, "Parent", null, null);
            db.StudyCatalogLessons.AddRange(first, second);
            db.StudyCatalogUnits.AddRange(unit, otherUnit);
            db.StudyCatalogTopics.Add(parent);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            switch (invalidCase)
            {
                case "duplicate-source":
                    db.StudyCatalogLessons.Add(StudyCatalogLesson.Create("test", "first", "Duplicate", 8, null));
                    break;
                case "wrong-lesson":
                    db.StudyCatalogTopics.Add(StudyCatalogTopic.Create("test", "wrong", second.Id, unit.Id, "Wrong", null, 1));
                    break;
                case "wrong-parent-unit":
                    db.StudyCatalogTopics.Add(StudyCatalogTopic.Create("test", "wrong", first.Id, otherUnit.Id, "Wrong", parent.Id, 1));
                    break;
            }
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.StudyCatalogLessons.CountAsync());
            Assert.Equal(1, await db.StudyCatalogTopics.CountAsync());
            Assert.False((await db.StudyCatalogTopics.SingleAsync()).IsActive);
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(current, previous));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.StudyCatalogLessons.CountAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
