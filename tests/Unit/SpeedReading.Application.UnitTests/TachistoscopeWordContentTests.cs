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
    public async Task Phrase_level_uses_random_seeded_pairs_near_every_length_in_range()
    {
        var (db, service, exercises) = await Arrange();
        await using var _ = db;
        var student = Guid.NewGuid();
        var started = await service.StartAsync(student, new() { ExerciseId = exercises[5] });
        var state = await State(db, started.SessionId);
        state.GetProperty("contentType").GetString().Should().Be("phrase");
        var pool = state.GetProperty("pool").EnumerateArray().Select(item => item.GetString()!).ToArray();
        var vocabulary = Vocabulary();
        var index = vocabulary.Select((word, position) => (word, position)).ToDictionary(item => item.word, item => item.position);

        pool.Should().OnlyContain(phrase => phrase.Split(' ').Length == 2
            && index.ContainsKey(phrase.Split(' ')[0]) && index.ContainsKey(phrase.Split(' ')[1]));
        for (var length = 10; length <= 16; length++)
            pool.Count(phrase => phrase.Length == length).Should().BeGreaterThanOrEqualTo(10, $"length {length} is in the adaptive range");
        var neighbours = pool.Count(phrase => index[phrase.Split(' ')[1]] - index[phrase.Split(' ')[0]] == 1);
        neighbours.Should().BeLessThan(pool.Length / 10, "pairs are random, not alphabetical neighbours");

        // The pairing is reproducible from the stored seed and differs between sessions.
        var seed = state.GetProperty("contentSeed").GetInt32();
        TachistoscopeContent.BuildPhrasePool(vocabulary, seed, 10, 16).Should().Equal(pool);
        var other = await service.StartAsync(Guid.NewGuid(), new() { ExerciseId = exercises[5] });
        var otherPool = (await State(db, other.SessionId)).GetProperty("pool").EnumerateArray().Select(item => item.GetString()!).ToArray();
        otherPool.Should().NotEqual(pool);

        var presented = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "tachistoscope_present", Index = 0 });
        presented.IsValid.Should().BeTrue(presented.Message);
        var stimulus = presented.FeedbackData!.Value.GetProperty("stimulus").GetString()!;
        stimulus.Length.Should().Be(12);
        stimulus.Split(' ').Should().HaveCount(2);
    }

    [Fact]
    public void The_phrase_pool_is_deterministic_for_a_seed_and_skips_identical_pairs()
    {
        var words = new[] { "ev", "su", "göz", "kapı", "masa", "kalem", "kitap", "pencere" };
        var first = TachistoscopeContent.BuildPhrasePool(words, 7, 6, 10);
        first.Should().Equal(TachistoscopeContent.BuildPhrasePool(words, 7, 6, 10));
        first.Should().OnlyHaveUniqueItems();
        first.Should().OnlyContain(phrase => phrase.Split(' ')[0] != phrase.Split(' ')[1]);
        for (var length = 6; length <= 10; length++) first.Should().Contain(phrase => phrase.Length == length);
        TachistoscopeContent.BuildPhrasePool(["ev"], 7, 6, 10).Should().BeEmpty();
    }
}
