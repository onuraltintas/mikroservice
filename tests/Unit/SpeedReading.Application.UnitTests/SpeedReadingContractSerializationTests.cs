using System.Text.Json;
using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Gamification;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingContractSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Exercise_session_metrics_keep_the_legacy_acronym_casing()
    {
        using var document = JsonDocument.Parse("{}");
        var result = new ExerciseSessionResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            0,
            100,
            60,
            90,
            100,
            300,
            250,
            200,
            10,
            [],
            false,
            null,
            document.RootElement.Clone(),
            "Tamamlandı.",
            null);

        var json = JsonSerializer.Serialize(result, JsonOptions);

        json.Should().Contain("\"rawWPM\":300");
        json.Should().Contain("\"weightedKDP\":200");
    }

    [Fact]
    public void Gamification_metrics_keep_the_legacy_acronym_casing()
    {
        var summary = new GamificationSummary(
            Guid.NewGuid(),
            900,
            9,
            0,
            100,
            "Okuyucu",
            "📖",
            2,
            3,
            null,
            1,
            10,
            30,
            DateTime.UtcNow,
            null);

        var json = JsonSerializer.Serialize(summary, JsonOptions);

        json.Should().Contain("\"totalXP\":900");
        json.Should().Contain("\"currentLevelXP\":0");
        json.Should().Contain("\"nextLevelXP\":100");
    }
}
