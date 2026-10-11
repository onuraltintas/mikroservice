using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ChildExerciseCatalogTests
{
    [Theory]
    [InlineData("child-exercises", "10000000-0000-0000-0000-000000000001", 20, 92)]
    [InlineData("teen-exercises", "10000000-0000-0000-0000-000000000002", 21, 96)]
    [InlineData("adult-exercises", "10000000-0000-0000-0000-000000000003", 21, 97)]
    [InlineData("young-adult-exercises", "10000000-0000-0000-0000-000000000004", 21, 97)]
    public void Catalog_entries_have_valid_unpinned_configurations(string pack, string ageGroupId, int typeCount, int count)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName,
            "content-packs", pack, "v1", "catalog.json")));
        var entries = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(count, entries.Length);
        Assert.Equal(count, entries.Select(entry => entry.GetProperty("id").GetGuid()).Distinct().Count());
        Assert.Equal(typeCount, entries.Select(entry => entry.GetProperty("type").GetString()).Distinct().Count());
        var distribution = pack switch {
            "child-exercises" => new[] { 16, 17, 18, 21, 20 },
            "teen-exercises" => new[] { 17, 17, 20, 21, 21 },
            _ => new[] { 17, 18, 20, 21, 21 }
        };
        foreach (var level in Enumerable.Range(1, 5))
            Assert.Equal(distribution[level - 1],
                entries.Count(entry => entry.GetProperty("difficultyLevel").GetInt32() == level));
        foreach (var entry in entries)
        {
            var config = entry.GetProperty("configuration");
            ExerciseConfigurationRules.ValidateActiveConfiguration(config.GetRawText(), config.GetProperty("engineType").GetString()!);
            Assert.False(config.TryGetProperty("readingTextId", out _));
            Assert.Equal(Guid.Parse(ageGroupId), entry.GetProperty("targetAgeGroupId").GetGuid());
            var content = config.GetProperty("engineConfig");
            if (content.TryGetProperty("content", out var source))
            {
                if (entry.GetProperty("type").GetString() is "RegressionReduction" or "SubvocalizationReduction" or "RSVP")
                {
                    Assert.True(source.GetProperty("minWordCount").GetInt32() > 0);
                    Assert.True(source.GetProperty("maxWordCount").GetInt32() >= source.GetProperty("minWordCount").GetInt32());
                    Assert.Equal(5, source.GetProperty("questionCount").GetInt32());
                }
                else if (entry.GetProperty("type").GetString() is "FreeReading" or "ErrorAnalysis")
                {
                    Assert.True(source.GetProperty("minWordCount").GetInt32() > 0);
                    Assert.True(source.GetProperty("maxWordCount").GetInt32() >= source.GetProperty("minWordCount").GetInt32());
                    Assert.False(source.TryGetProperty("questionCount", out _));
                }
                else
                {
                    Assert.False(source.TryGetProperty("minWordCount", out _));
                    Assert.False(source.TryGetProperty("maxWordCount", out _));
                }
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
