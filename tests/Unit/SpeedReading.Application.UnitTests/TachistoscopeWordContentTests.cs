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
/// Levels 3–4 show vocabulary words and level 5 two-word phrases. Stimuli stay near the configured
/// length, and phrases are random pairs fixed per session by a stored seed instead of alphabetical neighbours.
/// </summary>
public sealed class TachistoscopeWordContentTests
{
    private const string Letters = "abcçdefgğhıijklmnoöprsştuüvyz";

    private static List<string> Vocabulary()
    {
        var random = new Random(20261112);
        var words = new SortedSet<string>(StringComparer.Ordinal);
        for (var length = 2; length <= 12; length++)
            while (words.Count(word => word.Length == length) < 15)
                words.Add(string.Concat(Enumerable.Range(0, length).Select(_ => Letters[random.Next(Letters.Length)])));
        return [.. words];
    }

    private static string Config(string type, int length, int minLength, int maxLength, int duration, int level) =>
        $$$"""{"engineType":"text_stream","engineConfig":{"mode":"tachistoscope","content":{"type":"{{{type}}}","count":20,"length":{{{length}}}},"timing":{"durationMs":{{{duration}}},"intervalMs":500},"adaptive":{"enabled":true,"minLength":{{{minLength}}},"maxLength":{{{maxLength}}},"minDurationMs":250,"maxDurationMs":1500}},"difficultyLevel":{{{level}}}}""";

    private static async Task<(OwnedSpeedReadingDbContext Db, ISpeedReadingExerciseSessions Service, Dictionary<int, Guid> Exercises)> Arrange()
    {
        var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var actor = Guid.NewGuid();
        foreach (var word in Vocabulary())
            db.VocabularyItems.Add(VocabularyItem.Create(Guid.NewGuid(), word, "anlam " + word, null, null, null,
                "genel", 1, null, actor, DateTime.UtcNow));
        // Not letters only, or too long: never shown.
        db.VocabularyItems.Add(VocabularyItem.Create(Guid.NewGuid(), "çok-yönlü", "anlam", null, null, null, "genel", 1, null, actor, DateTime.UtcNow));
        var type = ExerciseType.Create(Guid.NewGuid(), "Tachistoscope", "Takistoskop", "text_stream");
        db.ExerciseTypes.Add(type);
        var exercises = new Dictionary<int, Guid>();
        foreach (var (level, config) in new[]
                 {
                     (3, Config("word", 6, 5, 9, 480, 3)),
                     (4, Config("word", 7, 6, 10, 440, 4)),
                     (5, Config("phrase", 12, 10, 16, 400, 5))
                 })
        {
            var exercise = Exercise.Create("Kısa Gösterimde Tanıma", "Tachistoscope", config, level, actor, type.Id);
            db.Exercises.Add(exercise);
            exercises[level] = exercise.Id;
        }
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        return (db, service, exercises);
    }

    private static async Task<JsonElement> State(OwnedSpeedReadingDbContext db, Guid sessionId)
    {
        var session = await db.ExerciseSessions.AsNoTracking().SingleAsync(item => item.Id == sessionId);
        return JsonDocument.Parse(session.SessionDataJson).RootElement.GetProperty("tachistoscope").Clone();
    }

    [Theory]
    [InlineData(3, 6)]
    [InlineData(4, 7)]
    public async Task Word_levels_show_vocabulary_words_of_the_target_length(int level, int length)
    {
        var (db, service, exercises) = await Arrange();
        await using var _ = db;
        var student = Guid.NewGuid();
        var started = await service.StartAsync(student, new() { ExerciseId = exercises[level] });
        var state = await State(db, started.SessionId);
        state.GetProperty("contentType").GetString().Should().Be("word");
        state.GetProperty("source").GetString().Should().Be("vocabulary_items");
        var pool = state.GetProperty("pool").EnumerateArray().Select(item => item.GetString()!).ToArray();
        pool.Should().OnlyContain(word => word.All(char.IsLetter) && !word.Contains(' '));
        pool.Should().NotContain("çok-yönlü");
        var presented = await service.ValidateActionAsync(student, started.SessionId,
            new() { Action = "tachistoscope_present", Index = 0 });
        presented.IsValid.Should().BeTrue(presented.Message);
        var stimulus = presented.FeedbackData!.Value.GetProperty("stimulus").GetString()!;
        stimulus.Length.Should().Be(length);
        stimulus.Should().NotContain(" ");
    }

    [Fact]
    public async Task Phrase_level_uses_meaningful_phrases_for_every_length_in_range()
    {
        var (db, service, exercises) = await Arrange();
        await using var _ = db;
        var student = Guid.NewGuid();
        var started = await service.StartAsync(student, new() { ExerciseId = exercises[5] });
        var state = await State(db, started.SessionId);
        state.GetProperty("contentType").GetString().Should().Be("phrase");
        state.GetProperty("source").GetString().Should().Be("phrase_list");
        var pool = state.GetProperty("pool").EnumerateArray().Select(item => item.GetString()!).ToArray();

        // Live test 19.11.2026: no random word pairs; only phrases from the list, each length of the range covered.
        pool.Should().OnlyContain(phrase => TachistoscopePhrases.All.Contains(phrase));
        for (var length = 10; length <= 16; length++)
            pool.Count(phrase => phrase.Length == length).Should().BeGreaterThanOrEqualTo(10, $"length {length} is in the adaptive range");

        // The order is reproducible from the stored seed and differs between sessions.
        var seed = state.GetProperty("contentSeed").GetInt32();
        TachistoscopePhrases.Pool(seed, 10, 16).Should().Equal(pool);
        var other = await service.StartAsync(Guid.NewGuid(), new() { ExerciseId = exercises[5] });
        var otherPool = (await State(db, other.SessionId)).GetProperty("pool").EnumerateArray().Select(item => item.GetString()!).ToArray();
        otherPool.Should().NotEqual(pool);

        var presented = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "tachistoscope_present", Index = 0 });
        presented.IsValid.Should().BeTrue(presented.Message);
        var stimulus = presented.FeedbackData!.Value.GetProperty("stimulus").GetString()!;
        stimulus.Length.Should().Be(12);
        TachistoscopePhrases.All.Should().Contain(stimulus);
    }

    [Fact]
    public void The_phrase_list_is_plain_two_word_turkish_and_covers_every_level_5_length()
    {
        TachistoscopePhrases.All.Should().OnlyHaveUniqueItems();
        TachistoscopePhrases.All.Should().OnlyContain(phrase => phrase.Split(' ').Length == 2
            && phrase.All(character => char.IsLetter(character) || character == ' ')
            && !phrase.Split(' ').Any(TachistoscopeWordExclusions.IsExcluded));
        for (var length = TachistoscopePhrases.MinimumLength; length <= TachistoscopePhrases.MaximumLength; length++)
            TachistoscopePhrases.All.Count(phrase => phrase.Length == length).Should().BeGreaterThanOrEqualTo(12, $"length {length}");
        TachistoscopePhrases.Pool(7, 6, 10).Should().Equal(TachistoscopePhrases.Pool(7, 6, 10));
        TachistoscopePhrases.Pool(7, 6, 10).Should().OnlyContain(phrase => phrase.Length >= 6 && phrase.Length <= 10);
        TachistoscopePhrases.Pool(7, 40, 50).Should().HaveCount(TachistoscopePhrases.All.Count);
    }
}
