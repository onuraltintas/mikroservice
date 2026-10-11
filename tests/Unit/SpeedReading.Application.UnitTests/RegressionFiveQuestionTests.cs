using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class RegressionFiveQuestionTests
{
    private const string Config = """{"engineType":"regression_reduction","engineConfig":{"targetWpm":220,"maskingType":"trailing","visibleHistoryWords":1,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":5,"maxWordCount":10}}}""";

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
    public async Task Snapshots_exactly_five_questions_and_resumes_without_replacing_history()
    {
        await using var db = Context();
        var (service, student, exercise, _) = await Seed(db, 7, 6);
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        Assert.Equal(5, started.InitialData.GetProperty("questions").GetArrayLength());
        var session = await db.ExerciseSessions.SingleAsync();
        var snapshot = session.SessionDataJson;
        db.Entry(await db.Exercises.SingleAsync()).Property(e => e.ConfigurationJson).CurrentValue = "{}";
        await db.SaveChangesAsync();
        Assert.Equal(started.SessionId, (await service.StartAsync(student, new() { ExerciseId = exercise })).SessionId);
        Assert.Equal(snapshot, session.SessionDataJson);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Rejects_invalid_history_window(int visible)
    {
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(
            Config.Replace("\"visibleHistoryWords\":1", $"\"visibleHistoryWords\":{visible}"), "regression_reduction"));
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions Service, Guid Student, Guid Exercise, Guid Text)> Seed(
        OwnedSpeedReadingDbContext db, int questions, int words)
    {
        var student = Guid.NewGuid(); var type = ExerciseType.Create(Guid.NewGuid(), "RegressionReduction", "Regresyon", "regression_reduction");
        var exercise = Exercise.Create("Regresyon", "reading", Config, 3, student, type.Id);
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
