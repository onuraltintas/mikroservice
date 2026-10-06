using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SubvocalizationPersistenceTests
{
    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

    private static Exercise Seed(OwnedSpeedReadingDbContext db, Guid student, bool questions = false, string engine = "subvocalization_reduction")
    {
        var type = ExerciseType.Create(Guid.NewGuid(), "SubvocalizationReduction", "İç Ses", engine);
        var exercise = Exercise.Create("İç Ses", "reading", questions
            ? """{"engineType":"subvocalization_reduction","engineConfig":{"readingPurpose":"evaluation"}}""" : "{}",
            3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        return exercise;
    }

    [Theory]
    [InlineData(false, "subvocalization_reduction")]
    [InlineData(true, "subvocalization_reduction")]
    [InlineData(false, "Subvocalization-Reduction")]
    public async Task Does_not_record_automatic_pace_as_reading_speed(bool questions, string engine)
    {
        await using var db = Context(); var student = Guid.NewGuid();
        var exercise = Seed(db, student, questions, engine);
        var text = ReadingText.Create(Guid.NewGuid(), "Text", "bir iki uc dort", difficultyLevel: 3);
        db.ReadingTexts.Add(text);
        var question = Guid.NewGuid();
        if (questions) db.ReadingQuestions.Add(ReadingQuestion.Create(question, text.Id, "Soru", "A", 0, 1, 1,
            optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
        await db.SaveChangesAsync(); var service = Service(db);
        var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-10);
        state["readingEndTime"] = DateTime.UtcNow.AddSeconds(-1);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        if (questions) Assert.True((await service.ValidateActionAsync(student, started.SessionId,
            new() { Action = "answer_question", QuestionId = question, Answer = "A" })).IsCorrect);
        var result = await service.CompleteAsync(student, started.SessionId, new());
        Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
        if (questions) Assert.Equal(100m, result.ComprehensionScore);
        else Assert.Null(result.ComprehensionScore);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_other_level_texts_for_automatic_and_explicit_selection(bool explicitText)
    {
        await using var db = Context(); var student = Guid.NewGuid(); var exercise = Seed(db, student);
        var text = ReadingText.Create(Guid.NewGuid(), "Wrong", "bir iki", difficultyLevel: 1);
        db.ReadingTexts.Add(text); await db.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => Service(db).StartAsync(student,
            new() { ExerciseId = exercise.Id, ReadingTextId = explicitText ? text.Id : null }));
        Assert.Empty(await db.ExerciseSessions.ToListAsync());
    }

    [Fact]
    public async Task Prefers_less_used_matching_level_text()
    {
        await using var db = Context(); var student = Guid.NewGuid(); var exercise = Seed(db, student);
        var first = ReadingText.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"), "First", "bir iki", difficultyLevel: 3);
        var second = ReadingText.Create(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), "Second", "uc dort", difficultyLevel: 3);
        db.ReadingTexts.AddRange(first, second);
        var past = ExerciseSession.Start(student, exercise.Id, first.Id, 2, DateTime.UtcNow.AddMinutes(-1), null);
        past.Complete(DateTime.UtcNow); db.ExerciseSessions.Add(past);
        await db.SaveChangesAsync();
        var started = await Service(db).StartAsync(student, new() { ExerciseId = exercise.Id });
        Assert.Equal(second.Id, (await db.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId)).ReadingTextId);
    }
}
