using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Takistoskop measures quick recognition of familiar words: its pools use everyday vocabulary
/// (engineConfig.content.maxVocabularyLevel caps the vocabulary level) and leave out a Takistoskop-only
/// exclusion list plus acronyms and mixed-case terms. Kelime Hazinesi itself is not affected.
/// </summary>
public sealed class TachistoscopeWordListTests
{
    private static readonly (string Word, int Level)[] Vocabulary =
    [
        ("kalem", 1), ("masa", 1), ("kapı", 1), ("bahçe", 1), ("deniz", 1), ("Hikâye", 1), ("Hikaye", 1), ("Savaş", 1),
        ("Ses", 1), ("not", 1), ("USB", 1), ("WiFi", 1), ("iPhone", 1), ("TikTok", 1), ("Özçekim", 2), ("Bej", 2),
        ("pencere", 2), ("sokak", 2), ("bulut", 2), ("çiçek", 2), ("kitap", 2), ("orman", 2),
        ("müstesna", 3), ("tevazu", 3), ("istisnai", 4), ("muhtelif", 5), ("Kaos", 3)
    ];

    private static readonly string[] Excluded = ["Hikaye", "Savaş", "Ses", "not", "USB", "WiFi", "iPhone", "TikTok", "Özçekim", "Bej", "Kaos"];

    private static string Config(string type, int length, int minLength, int maxLength, int? maxVocabularyLevel) =>
        $$$"""{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"{{{type}}}","count":20,"length":{{{length}}}{{{(maxVocabularyLevel is { } cap ? $",\"maxVocabularyLevel\":{cap}" : "")}}}},"timing":{"durationMs":400,"intervalMs":500},"adaptive":{"enabled":true,"minLength":{{{minLength}}},"maxLength":{{{maxLength}}},"minDurationMs":250,"maxDurationMs":1500}},"difficultyLevel":5}""";

    private static async Task<string[]> StartPool(string configuration)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var actor = Guid.NewGuid();
        foreach (var (word, level) in Vocabulary)
            db.VocabularyItems.Add(VocabularyItem.Create(Guid.NewGuid(), word, "anlam " + word, null, null, null, "genel", level, null, actor, DateTime.UtcNow));
        var type = ExerciseType.Create(Guid.NewGuid(), "Tachistoscope", "Takistoskop", "text_stream");
        var exercise = Exercise.Create("Kısa Gösterimde Tanıma", "Tachistoscope", configuration, 5, actor, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var started = await service.StartAsync(Guid.NewGuid(), new() { ExerciseId = exercise.Id });
        var session = await db.ExerciseSessions.AsNoTracking().SingleAsync(item => item.Id == started.SessionId);
        var state = JsonDocument.Parse(session.SessionDataJson).RootElement.GetProperty("tachistoscope");
        state.GetProperty("source").GetString().Should().Be("vocabulary_items");
        return state.GetProperty("pool").EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static readonly string[] Everyday = Vocabulary.Where(item => item.Level <= 2 && !Excluded.Contains(item.Word))
        .Select(item => item.Word).ToArray();

    [Fact]
    public async Task A_level_five_word_session_uses_only_everyday_words_without_excluded_ones()
    {
        var pool = await StartPool(Config("word", 6, 4, 8, 2));
        pool.Should().BeEquivalentTo(Everyday);
        pool.Should().Contain("Hikâye");
    }

    [Fact]
    public async Task Phrases_are_built_only_from_everyday_words_without_excluded_ones()
    {
        var pool = await StartPool(Config("phrase", 12, 10, 16, 2));
        pool.Should().NotBeEmpty();
        pool.SelectMany(phrase => phrase.Split(' ')).Should().OnlyContain(word => Everyday.Contains(word));
    }

    [Fact]
    public async Task Without_the_vocabulary_cap_the_exercise_level_still_decides()
    {
        var pool = await StartPool(Config("word", 6, 4, 8, null));
        pool.Should().Contain(["müstesna", "tevazu", "istisnai", "muhtelif"]);
        pool.Should().NotIntersectWith(Excluded);
        pool.Should().Contain("Hikâye");
    }

    [Theory]
    [InlineData("Hikaye", true)]
    [InlineData("hikaye", true)]
    [InlineData("Hikâye", false)]
    [InlineData("SAVAŞ", true)]
    [InlineData("savaş", true)]
    [InlineData("Özçekim", true)]
    [InlineData("Mısra", true)]
    [InlineData("GIF", true)]
    [InlineData("WiFi", true)]
    [InlineData("iPhone", true)]
    [InlineData("TikTok", true)]
    [InlineData("YouTube", true)]
    [InlineData("Kalem", false)]
    [InlineData("kalem", false)]
    [InlineData("İstanbul", false)]
    [InlineData("Seslenmek", false)]
    public void The_exclusion_list_matches_whole_words_and_drops_acronyms_and_mixed_case(string word, bool excluded)
    {
        TachistoscopeWordExclusions.IsExcluded(word).Should().Be(excluded);
    }

    [Fact]
    public void The_exclusion_list_holds_the_approved_68_words()
    {
        TachistoscopeWordExclusions.Words.Should().HaveCount(68);
        TachistoscopeWordExclusions.Words.Should().Contain(["Bej", "Hikaye", "Özçekim", "Savaş", "VPN"]);
        TachistoscopeWordExclusions.Words.Should().NotContain("Hikâye");
    }
}
