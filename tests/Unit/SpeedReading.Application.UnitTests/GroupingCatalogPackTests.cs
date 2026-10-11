using System.Text.Json;
using FluentAssertions;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class GroupingCatalogPackTests
{
    internal static string PackDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "content-packs", "grouping", "v1");
    }

    [Fact]
    public void Twenty_age_level_settings_and_real_text_pools_match_the_sql_payload()
    {
        var directory = PackDirectory();
        var settingsJson = File.ReadAllText(Path.Combine(directory, "settings.json"));
        var contentJson = File.ReadAllText(Path.Combine(directory, "content.json"));
        using var settings = JsonDocument.Parse(settingsJson);
        using var content = JsonDocument.Parse(contentJson);
        var sql = File.ReadAllText(Path.Combine(directory, "apply.sql"));
        sql.Should().Contain(settingsJson.Trim()).And.Contain(contentJson.Trim());
        var rows = settings.RootElement.EnumerateArray().ToArray();
        rows.Should().HaveCount(20);
        rows.Select(row => (row.GetProperty("ageGroupId").GetGuid(), row.GetProperty("level").GetInt32()))
            .Should().OnlyHaveUniqueItems();
        foreach (var row in rows)
        {
            var config = row.GetProperty("configuration");
            ExerciseConfigurationRules.ValidateActiveConfiguration(config.GetRawText(), "word_highlight");
            var engine = config.GetProperty("engineConfig");
            engine.GetProperty("readingPurpose").GetString().Should().Be("practice");
            engine.TryGetProperty("timing", out _).Should().BeFalse();
            var minimum = engine.GetProperty("content").GetProperty("minWordCount").GetInt32();
            var maximum = engine.GetProperty("content").GetProperty("maxWordCount").GetInt32();
            var texts = content.RootElement.EnumerateArray().Where(text => text.GetProperty("exerciseId").GetGuid() == row.GetProperty("id").GetGuid()).ToArray();
            texts.Should().HaveCount(3);
            texts.Select(text => text.GetProperty("content").GetString()).Should().OnlyHaveUniqueItems();
            foreach (var text in texts)
            {
                var words = System.Text.RegularExpressions.Regex.Matches(text.GetProperty("content").GetString()!, @"\S+").Count;
                words.Should().BeInRange(minimum, maximum);
                words.Should().Be(text.GetProperty("wordCount").GetInt32());
                text.GetProperty("level").GetInt32().Should().Be(row.GetProperty("level").GetInt32());
                text.GetProperty("ageGroupId").GetGuid().Should().Be(row.GetProperty("ageGroupId").GetGuid());
            }
        }
    }
}
