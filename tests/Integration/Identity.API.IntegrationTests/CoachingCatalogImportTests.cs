using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingCatalogImportTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Import_IsAtomicInactiveAndRepeatableAndRejectsChangedContent()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var files = new Dictionary<string, string>
            {
                ["lessons.json"] = """[{"id":"l","name":"Lesson","gradeName":"TYT"}]""",
                ["units-derived.json"] = """[{"id":"u","name":"Unit","lessonId":"l","displayOrder":null}]""",
                ["upper-subjects.json"] = """[{"id":"p","name":"Parent","unitId":"u","lessonId":"l","rank":1}]""",
                ["subjects.json"] = """[{"id":"t","name":"Topic","unitId":"u","upperSubjectId":"p","rank":2}]""",
                ["university-programs.json"] = """[{"id":"p1","university":"University","programName":"Program","programCode":"123","scoreType":"SAY","minScore":400,"scoreYear":2025}]""",
                ["lgs-programs.json"] = """[{"id":"s1","schoolName":"School","city":"City","town":"District","minScore":350}]"""
            };
            var importer = new CoachingCatalogImporter(db);
            // Opt-in manual validation reads only catalog files, never private student exports.
            var providedDirectory = Environment.GetEnvironmentVariable("COACHING_TEST_CATALOG_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(providedDirectory))
            {
                Assert.Empty((await CatalogPreflight.ReadAsync(providedDirectory)).Errors);
                foreach (var name in files.Keys.ToArray())
                    files[name] = await File.ReadAllTextAsync(Path.Combine(providedDirectory, name));
                var expected = files.Values.Sum(json =>
                {
                    using var document = System.Text.Json.JsonDocument.Parse(json);
                    return document.RootElement.GetArrayLength();
                });
                Assert.Equal(expected, await importer.ImportAsync(files, "provided-catalog"));
                Assert.Equal(0, await importer.ImportAsync(files, "provided-catalog"));
                Assert.False(await db.StudyCatalogLessons.AnyAsync(x => x.IsActive));
                Assert.False(await db.TargetUniversityPrograms.AnyAsync(x => x.IsActive));
                Console.WriteLine($"Provided catalog records imported and repeated safely: {expected}");
                return;
            }
            Assert.Equal(6, await importer.ImportAsync(files, "test-catalog"));
            Assert.Equal(2025, (await db.TargetUniversityPrograms.SingleAsync()).ScoreYear);
            var lessonId = (await db.StudyCatalogLessons.SingleAsync()).Id;
            db.ChangeTracker.Clear();
            Assert.Equal(0, await importer.ImportAsync(files, "test-catalog"));
            Assert.Equal(lessonId, (await db.StudyCatalogLessons.SingleAsync()).Id);
            Assert.False((await db.TargetSchools.SingleAsync()).IsActive);
            db.ChangeTracker.Clear();
            files["lessons.json"] = """[{"id":"l","name":"Changed","gradeName":"TYT"},{"id":"new","name":"New","gradeName":"TYT"}]""";
            await Assert.ThrowsAsync<InvalidOperationException>(() => importer.ImportAsync(files, "test-catalog"));
            Assert.Equal(1, await db.StudyCatalogLessons.CountAsync());
            Assert.Equal("Lesson", (await db.StudyCatalogLessons.SingleAsync()).Name);
            db.ChangeTracker.Clear();
            files["lessons.json"] = """[{"id":"l","name":"Lesson","gradeName":"TYT"},{"id":"new","name":"New","gradeName":"TYT"}]""";
            files["lgs-programs.json"] = """[{"id":"s1","schoolName":"School","city":"City","town":"District","minScore":-5}]""";
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => importer.ImportAsync(files, "test-catalog"));
            Assert.Equal(1, await db.StudyCatalogLessons.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
