using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ChildExerciseCatalogTests
{
    [Fact]
    public void All_seventy_catalog_entries_have_valid_unpinned_configurations()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName,
            "content-packs", "child-exercises", "v1", "catalog.json")));
        var entries = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(70, entries.Length);
        Assert.Equal(70, entries.Select(entry => entry.GetProperty("id").GetGuid()).Distinct().Count());
        foreach (var level in Enumerable.Range(1, 5))
            Assert.Equal(14, entries.Count(entry => entry.GetProperty("difficultyLevel").GetInt32() == level));
        foreach (var entry in entries)
        {
            var config = entry.GetProperty("configuration");
            ExerciseConfigurationRules.ValidateActiveConfiguration(config.GetRawText(), config.GetProperty("engineType").GetString()!);
            Assert.False(config.TryGetProperty("readingTextId", out _));
            Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), entry.GetProperty("targetAgeGroupId").GetGuid());
            var content = config.GetProperty("engineConfig");
            if (content.TryGetProperty("content", out var source))
            {
                Assert.False(source.TryGetProperty("minWordCount", out _));
                Assert.False(source.TryGetProperty("maxWordCount", out _));
            }
        }
    }

    [Fact]
    public void Random_order_and_combined_history_translate_to_postgres()
    {
        using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        var student = Guid.NewGuid();
        var sql = db.ReadingTexts.OrderBy(text => db.ExerciseSessions.Count(history => history.StudentId == student && history.ReadingTextId == text.Id)
                + db.ReadingSessions.Count(history => history.UserId == student && history.ReadingTextId == text.Id))
            .ThenBy(_ => Guid.NewGuid()).Select(text => text.Id).Take(1).ToQueryString();
        Assert.Contains("gen_random_uuid()", sql);
        Assert.Contains("reading_sessions", sql);
        Assert.Contains("exercise_sessions", sql);
    }
}
