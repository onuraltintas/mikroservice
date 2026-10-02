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
            var review = await importer.ReviewAsync(files, "test-catalog");
            Assert.Equal(6, review.NewRecords);
            Assert.Equal(6, review.Counts.Values.Sum());
            await Assert.ThrowsAsync<ArgumentException>(() => importer.ApproveAsync(files, "test-catalog", review.Fingerprint, "", Guid.NewGuid(), false));
            await Assert.ThrowsAsync<ArgumentException>(() => importer.ApproveAsync(files, "test-catalog", review.Fingerprint, new string('x', 201), Guid.NewGuid(), false));
            var altered = files.ToDictionary(x => x.Key, x => x.Value);
            altered["lessons.json"] = altered["lessons.json"].Replace("Lesson", "Altered");
            await Assert.ThrowsAsync<InvalidOperationException>(() => importer.ApproveAsync(altered, "test-catalog", review.Fingerprint, "Test onayı", Guid.NewGuid(), false));
            Assert.Equal(6, await importer.PreviewAsync(files, "test-catalog"));
            Assert.False(await db.StudyCatalogLessons.AnyAsync());
            Assert.False(await db.TargetSchools.AnyAsync());
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
            Assert.Equal(6, await importer.ApproveAsync(files, "test-catalog", review.Fingerprint, "Test onayı", Guid.NewGuid(), false));
            Assert.Equal(1, await db.AdminAuditRecords.CountAsync(x => x.Action == "CatalogImport"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => importer.ApproveAsync(files, "test-catalog", review.Fingerprint, "Yayın onayı", Guid.NewGuid(), true));
            review = await importer.ReviewAsync(files, "test-catalog");
            var extra = Coaching.Domain.Entities.TargetSchool.Create("test-catalog", "outside-review", "Extra school", "City", "District", 350, null);
            db.TargetSchools.Add(extra); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var extraReview = await importer.ReviewAsync(files, "test-catalog");
            await Assert.ThrowsAsync<InvalidOperationException>(() => importer.ApproveAsync(files, "test-catalog", extraReview.Fingerprint, "Yayın onayı", Guid.NewGuid(), true));
            db.TargetSchools.Remove(extra); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            review = await importer.ReviewAsync(files, "test-catalog");
            Assert.Equal(6, await importer.ApproveAsync(files, "test-catalog", review.Fingerprint, "Yayın onayı", Guid.NewGuid(), true));
            Assert.True(await db.TargetSchools.AllAsync(x => x.IsActive));
            Assert.Equal(1, await db.AdminAuditRecords.CountAsync(x => x.Action == "CatalogPublish"));
            await new CoachingCatalogPublication(db).SetPublishedAsync("test-catalog", files, false, true);
            Assert.Equal(0, await importer.PreviewAsync(files, "test-catalog"));
            Assert.Equal(2025, (await db.TargetUniversityPrograms.SingleAsync()).ScoreYear);
            var lessonId = (await db.StudyCatalogLessons.SingleAsync()).Id;
            db.ChangeTracker.Clear();
            Assert.Equal(0, await importer.ImportAsync(files, "test-catalog"));
            Assert.Equal(lessonId, (await db.StudyCatalogLessons.SingleAsync()).Id);
            Assert.False((await db.TargetSchools.SingleAsync()).IsActive);
            db.ChangeTracker.Clear();
            var publication = new CoachingCatalogPublication(db);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => publication.SetPublishedAsync("test-catalog", files, true, false));
            Assert.False(await db.StudyCatalogLessons.AnyAsync(x => x.IsActive));
            var wrongContent = files.ToDictionary(x => x.Key, x => x.Value);
            wrongContent["lessons.json"] = wrongContent["lessons.json"].Replace("Lesson", "Unreviewed");
            await Assert.ThrowsAsync<InvalidOperationException>(() => publication.SetPublishedAsync("test-catalog", wrongContent, true, true));
            Assert.False(await db.StudyCatalogLessons.AnyAsync(x => x.IsActive));
            Assert.Equal(6, await publication.SetPublishedAsync("test-catalog", files, true, true));
            Assert.True(await db.StudyCatalogLessons.AllAsync(x => x.IsActive));
            Assert.True(await db.StudyCatalogUnits.AllAsync(x => x.IsActive));
            Assert.True(await db.StudyCatalogTopics.AllAsync(x => x.IsActive));
            Assert.True(await db.TargetUniversityPrograms.AllAsync(x => x.IsActive));
            Assert.True(await db.TargetSchools.AllAsync(x => x.IsActive));
            Assert.Equal(0, await publication.SetPublishedAsync("test-catalog", files, true, true));
            Assert.Equal(6, await publication.SetPublishedAsync("test-catalog", files, false, true));
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
            db.ChangeTracker.Clear();
            var school = await db.TargetSchools.SingleAsync();
            db.TargetSchools.Remove(school);
            db.AdminAuditRecords.Add(new(Guid.NewGuid(), DateTimeOffset.UtcNow, "Coaching", Guid.NewGuid().ToString(),
                "SystemAdmin", null, "DELETE", "/test", 200, "test", null, null, "CatalogPermanentDelete", "Schools", school.Id.ToString(),
                System.Text.Json.JsonSerializer.Serialize(new { source = "test-catalog", sourceId = "s1" })));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            files["lgs-programs.json"] = """[{"id":"s1","schoolName":"School","city":"City","town":"District","minScore":350}]""";
            await Assert.ThrowsAsync<InvalidOperationException>(() => importer.ImportAsync(files, "test-catalog"));
            Assert.False(await db.TargetSchools.AnyAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => importer.PreviewAsync(files, "test-catalog"));
            Assert.Equal(1, await db.StudyCatalogLessons.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
