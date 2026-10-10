using System.Text.Json.Nodes;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class ComprehensionSettingsPackTests
{
    [Fact]
    public void Forty_exercises_have_both_modes_at_every_age_and_level_with_approved_pilot_bands()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "content-packs", "comprehension", "v1", "settings.json")))!.AsArray();
        Assert.Equal(40, settings.Count);
        Assert.Equal(40, settings.Select(row => row!["id"]!.GetValue<Guid>()).Distinct().Count());
        var matrix = new Dictionary<string, (int Min, int Max, int Questions)[]> {
            ["child"] = [(180,250,5),(220,300,6),(280,360,6),(340,430,7),(400,500,8)],
            ["teen"] = [(300,400,6),(350,450,6),(400,550,7),(500,650,8),(600,750,8)],
            ["young-adult"] = [(400,550,6),(500,650,7),(600,750,8),(700,900,8),(850,1050,10)],
            ["adult"] = [(450,600,6),(550,700,7),(650,850,8),(800,1000,10),(950,1200,10)] };
        foreach (var (age, bands) in matrix)
        {
            var originals = JsonNode.Parse(File.ReadAllText(Path.Combine(directory.FullName, "content-packs", $"{age}-exercises", "v1", "catalog.json")))!.AsArray();
            for (var index = 0; index < bands.Length; index++)
            {
                var rows = settings.Where(row => row!["age"]!.GetValue<string>() == age && row["level"]!.GetValue<int>() == index + 1).ToArray();
                Assert.Equal(2, rows.Length);
                Assert.Equal(new[] { "evaluation", "learning" }, rows.Select(row => row!["configuration"]!["engineConfig"]!["comprehensionMode"]!.GetValue<string>()).Order().ToArray());
                foreach (var row in rows)
                {
                    var original = originals.Single(item => item!["id"]!.GetValue<string>() == row!["id"]!.GetValue<string>())!;
                    Assert.Equal("Comprehension", original["type"]!.GetValue<string>());
                    Assert.True(JsonNode.DeepEquals(original["configuration"], row!["previousConfiguration"]));
                    Assert.Equal(original["targetAgeGroupId"]!.GetValue<string>(), row!["ageGroupId"]!.GetValue<string>());
                    var config = row["configuration"]!;
                    ExerciseConfigurationRules.ValidateActiveConfiguration(config.ToJsonString(), "reading_comprehension");
                    var engine = config["engineConfig"]!;
                    Assert.Equal(bands[index].Min, engine["content"]!["minWordCount"]!.GetValue<int>());
                    Assert.Equal(bands[index].Max, engine["content"]!["maxWordCount"]!.GetValue<int>());
                    Assert.Equal(bands[index].Questions, engine["content"]!["questionCount"]!.GetValue<int>());
                    Assert.Equal(0, engine["timing"]!["minReadingTimeMs"]!.GetValue<int>());
                    Assert.Equal(0, engine["timing"]!["maxReadingTimeMs"]!.GetValue<int>());
                }
            }
        }
    }
}
