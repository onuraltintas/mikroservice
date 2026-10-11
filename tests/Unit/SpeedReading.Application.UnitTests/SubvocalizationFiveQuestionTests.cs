using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SubvocalizationFiveQuestionTests
{
    private const string Config = """{"engineType":"subvocalization_reduction","engineConfig":{"targetWpm":210,"chunkSize":2,"metronomeEnabled":true,"visualMetronome":true,"metronomeBpm":60,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":5,"maxWordCount":10}}}""";

    [Theory]
    [InlineData(false, 4, 6)]
    [InlineData(true, 4, 6)]
    [InlineData(false, 5, 4)]
    [InlineData(true, 5, 4)]
    [InlineData(false, 5, 11)]
    [InlineData(true, 5, 11)]
    public async Task Rejects_insufficient_questions_or_out_of_band_text(bool explicitText, int questions, int words)
    {
        await using var db = Context();
        var (service, student, exercise, text) = await Seed(db, questions, words);
        await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync(student, new() {
            ExerciseId = exercise, ReadingTextId = explicitText ? text : null }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Fact]
    public async Task Snapshots_five_valid_questions_and_keeps_existing_session()
    {
        await using var db = Context();
        var (service, student, exercise, text) = await Seed(db, 7, 6);
        db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text, "Eksik seçenek", "A", 0,
            type: 1, bloomLevel: 1, optionA: "A", optionB: "", optionC: "C", optionD: "D"));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        var snapshot = started.InitialData.GetProperty("questions");
        Assert.Equal(5, snapshot.GetArrayLength());
        Assert.All(snapshot.EnumerateArray(), question => Assert.NotEqual("Eksik seçenek", question.GetProperty("questionText").GetString()));
        var session = await db.ExerciseSessions.SingleAsync();
        var previous = session.SessionDataJson;
        db.Entry(await db.Exercises.SingleAsync()).Property(e => e.ConfigurationJson).CurrentValue = "{}";
        await db.SaveChangesAsync();
        Assert.Equal(started.SessionId, (await service.StartAsync(student, new() { ExerciseId = exercise })).SessionId);
        Assert.Equal(previous, session.SessionDataJson);
    }

    [Theory]
    [InlineData("\"questionCount\":4,\"minWordCount\":5,\"maxWordCount\":10")]
    [InlineData("\"questionCount\":5,\"minWordCount\":11,\"maxWordCount\":10")]
    public void Rejects_invalid_content_configuration(string content)
    {
        var configuration = "{\"engineType\":\"subvocalization_reduction\",\"engineConfig\":{\"content\":{" + content + "}}}";
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(configuration, "subvocalization_reduction"));
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions Service, Guid Student, Guid Exercise, Guid Text)> Seed(
        OwnedSpeedReadingDbContext db, int questions, int words)
    {
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "SubvocalizationReduction", "İç Ses Azaltma", "subvocalization_reduction");
        var exercise = Exercise.Create("İç Ses Azaltma", "reading", Config, 3, student, type.Id);
        var text = ReadingText.Create(Guid.NewGuid(), "Deneme", string.Join(' ', Enumerable.Repeat("kelime", words)), difficultyLevel: 3);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.ReadingTexts.Add(text);
        for (var index = 0; index < questions; index++) db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id,
            $"Soru {index}", "A", index, type: 1, bloomLevel: 1, optionA: "Doğru", optionB: "B", optionC: "C", optionD: "D"));
        await db.SaveChangesAsync();
        var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        return (service, student, exercise.Id, text.Id);
    }
}
