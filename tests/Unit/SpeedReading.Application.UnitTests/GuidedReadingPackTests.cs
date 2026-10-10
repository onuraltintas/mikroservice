using System.Text.Json.Nodes;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class GuidedReadingPackTests
{
    [Fact]
    public void Pack_preserves_twenty_speeds_and_declares_valid_duration_and_text_bands()
    {
        var root = FindRoot();
        var entries = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "content-packs/guided-reading/v1/settings.json")))!.AsArray();
        Assert.Equal(20, entries.Count);
        Assert.Equal(20, entries.Select(e => e!["id"]!.GetValue<string>()).Distinct().Count());
        foreach (var entry in entries)
        {
            var config = entry!["configuration"]!;
            ExerciseConfigurationRules.ValidateActiveConfiguration(config.ToJsonString(), "word_highlight");
            var settings = config["engineConfig"]!;
            var speed = settings["pacer"]!["speedWpm"]!.GetValue<int>();
            Assert.Equal(entry["previousConfiguration"]!["engineConfig"]!["pacer"]!["speedWpm"]!.GetValue<int>(), speed);
            Assert.Equal(1, settings["pacer"]!["chunkSize"]!.GetValue<int>());
            Assert.Equal("speed_reading", settings["mode"]!.GetValue<string>());
            Assert.Equal("practice", settings["readingPurpose"]!.GetValue<string>());
            Assert.False(settings["adaptive"]!["enabled"]!.GetValue<bool>());
            Assert.Equal(speed * entry["minMinutes"]!.GetValue<decimal>(), settings["content"]!["minWordCount"]!.GetValue<int>());
            Assert.Equal(speed * entry["maxMinutes"]!.GetValue<decimal>(), settings["content"]!["maxWordCount"]!.GetValue<int>());
            Assert.Null(settings["timing"]);
        }
    }

    [Fact]
    public void Original_texts_are_distinct_in_band_and_match_the_checked_sql_payload()
    {
        var root = FindRoot();
        var entries = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "content-packs/guided-reading/v1/content.json")))!.AsArray();
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "content-packs/guided-reading/v1/settings.json")))!.AsArray();
        Assert.Equal(22, entries.Count);
        Assert.Equal(22, entries.Select(e => e!["id"]!.GetValue<string>()).Distinct().Count());
        var sql = File.ReadAllText(Path.Combine(root, "content-packs/guided-reading/v1/apply.sql"));
        var originals = new HashSet<string>();
        foreach (var entry in entries)
        {
            Assert.Contains(entry!["id"]!.GetValue<string>(), sql);
            var exercise = settings.Single(e => e!["id"]!.GetValue<string>() == entry["exerciseId"]!.GetValue<string>())!;
            var bounds = exercise["configuration"]!["engineConfig"]!["content"]!;
            if (entry["file"] is not null)
            {
                var content = File.ReadAllText(Path.Combine(root, "content-packs/guided-reading/v1/texts", entry["file"]!.GetValue<string>())).Trim();
                Assert.True(originals.Add(content));
                Assert.InRange(ReadingText.CalculateWordCount(content), bounds["minWordCount"]!.GetValue<int>(), bounds["maxWordCount"]!.GetValue<int>());
                Assert.Contains(JsonValue.Create(content)!.ToJsonString(), sql);
            }
            else Assert.Equal(2, entry["sources"]!.AsArray().Count);
        }
        Assert.Equal(8, originals.Count);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
