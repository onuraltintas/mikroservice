using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ExamSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsDifferentLevelText(bool explicitText)
    {
        await using var db = Context();
        var (service, student, exercise, text, _) = await Seed(db, 1);
        await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync(student,
            new() { ExerciseId = exercise, ReadingTextId = explicitText ? text : null }));
    }

    [Fact]
    public async Task RequiresServerQuestionStartAndDoesNotTrustClientResponseTime()
    {
        await using var db = Context();
        var (service, student, exercise, _, question) = await Seed(db, 3);
        var start = await service.StartAsync(student, new() { ExerciseId = exercise });
        Assert.False((await service.ValidateActionAsync(student, start.SessionId,
            new() { Action = "answer_question", QuestionId = question, Answer = "A", ResponseTime = 999999 })).IsValid);
        await Read(service, student, start.SessionId);
        Assert.True((await service.ValidateActionAsync(student, start.SessionId,
            new() { Action = "exam_question_start", QuestionId = question })).IsValid);
        Assert.False((await service.ValidateActionAsync(student, start.SessionId,
            new() { Action = "answer_question", QuestionId = question, IsTimeout = true })).IsValid);
        Assert.True((await service.ValidateActionAsync(student, start.SessionId,
            new() { Action = "answer_question", QuestionId = question, Answer = "A", ResponseTime = 999999 })).IsValid);
        var result = await service.CompleteAsync(student, start.SessionId, new());
        Assert.Null(result.RawWPM);
        Assert.Equal(100, result.ComprehensionScore);
        var state = JsonNode.Parse((await db.ExerciseSessions.SingleAsync()).SessionDataJson)!;
        Assert.InRange(state["answers"]![0]!["timeSpentSeconds"]!.GetValue<int>(), 0, 5);
        await service.CompleteAsync(student, start.SessionId, new());
        Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
    }

    [Fact]
    public async Task LateAnswerBecomesBlankAndRepeatedStartDoesNotResetClock()
    {
        await using var db = Context();
        var (service, student, exercise, _, question) = await Seed(db, 3);
        var start = await service.StartAsync(student, new() { ExerciseId = exercise });
        await Read(service, student, start.SessionId);
        await service.ValidateActionAsync(student, start.SessionId, new() { Action = "exam_question_start", QuestionId = question });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["examQuestionStartedAt"] = DateTime.UtcNow.AddSeconds(-61);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var repeatedStart = await service.ValidateActionAsync(student, start.SessionId, new() { Action = "exam_question_start", QuestionId = question });
        Assert.Equal(0, repeatedStart.FeedbackData!.Value.GetProperty("remainingSeconds").GetInt32());
        var answer = await service.ValidateActionAsync(student, start.SessionId, new() { Action = "answer_question", QuestionId = question, Answer = "A" });
        Assert.Null(answer.IsCorrect);
        Assert.True(answer.FeedbackData!.Value.GetProperty("timedOut").GetBoolean());
        var result = await service.CompleteAsync(student, start.SessionId, new());
        Assert.Equal(0, result.ComprehensionScore); Assert.Null(result.RawWPM);
    }

    [Fact]
    public async Task UsesConfiguredQuestionDuration()
    {
        await using var db = Context();
        var (service, student, exercise, _, _) = await Seed(db, 3);
        var record = await db.Exercises.SingleAsync();
        record.Update(record.Title, record.Description, record.TypeCode, """{"engineConfig":{"timing":{"questionTimeSeconds":30}}}""", 3, record.ExerciseTypeId, null, student, DateTime.UtcNow);
        await db.SaveChangesAsync();
        var start = await service.StartAsync(student, new() { ExerciseId = exercise });
        Assert.Equal(30, start.InitialData.GetProperty("examQuestionTimeSeconds").GetInt32());
    }

    [Fact]
    public async Task ReadingMustFinishBeforeQuestionsAndAnswersAreHiddenUntilCompletion()
    {
        await using var db = Context();
        var (service, student, exercise, _, question) = await Seed(db, 3);
        var start = await service.StartAsync(student, new() { ExerciseId = exercise });
        Assert.False((await service.ValidateActionAsync(student, start.SessionId,
            new() { Action = "exam_question_start", QuestionId = question })).IsValid);
        await Read(service, student, start.SessionId);
        await service.ValidateActionAsync(student, start.SessionId, new() { Action = "exam_question_start", QuestionId = question });
        var answer = await service.ValidateActionAsync(student, start.SessionId, new() { Action = "answer_question", QuestionId = question, Answer = "A" });
        Assert.Null(answer.IsCorrect); Assert.Null(answer.CorrectAnswer); Assert.Null(answer.Explanation);
        var retry = await service.ValidateActionAsync(student, start.SessionId, new() { Action = "answer_question", QuestionId = question, Answer = "B" });
        Assert.Null(retry.IsCorrect); Assert.Null(retry.CorrectAnswer);
        var result = await service.CompleteAsync(student, start.SessionId, new());
        Assert.Equal("A", result.DetailedResults.GetProperty("questions")[0].GetProperty("correctAnswer").GetString());
        Assert.Equal(100, result.Score); Assert.Null(result.RawWPM);
    }

    [Fact]
    public async Task CannotPauseExamOnServer()
    {
        await using var db = Context();
        var (service, student, exercise, _, _) = await Seed(db, 3);
        var start = await service.StartAsync(student, new() { ExerciseId = exercise });
        await Assert.ThrowsAnyAsync<Exception>(() => service.PauseAsync(student, start.SessionId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsOutOfBandText(bool explicitText)
    {
        await using var db = Context();
        var (service, student, exercise, text, _) = await Seed(db, 3);
        await Configure(db, student, """{"content":{"minWordCount":100,"maxWordCount":200}}""");
        await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync(student,
            new() { ExerciseId = exercise, ReadingTextId = explicitText ? text : null }));
    }

    [Fact]
    public async Task RejectsInsufficientQuestions()
    {
        await using var db = Context();
        var (service, student, exercise, _, _) = await Seed(db, 3);
        await Configure(db, student, """{"content":{"questionCount":2}}""");
        await Assert.ThrowsAnyAsync<Exception>(() => service.StartAsync(student, new() { ExerciseId = exercise }));
    }

    [Fact]
    public async Task EnforcesReadingMinimumAndCapsReadingTimeWithoutCountingBreaks()
    {
        await using var db = Context();
        var (service, student, exercise, _, question) = await Seed(db, 3);
        await Configure(db, student, """{"timing":{"minReadingTimeMs":10000,"maxReadingTimeMs":30000}}""");
        var start = await service.StartAsync(student, new() { ExerciseId = exercise });
        await service.ValidateActionAsync(student, start.SessionId, new() { Action = "start_reading" });
        Assert.False((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "finish_reading" })).IsValid);
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-40);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        Assert.True((await service.ValidateActionAsync(student, start.SessionId, new() { Action = "finish_reading" })).IsValid);
        await service.ValidateActionAsync(student, start.SessionId, new() { Action = "exam_question_start", QuestionId = question });
        state = JsonNode.Parse(session.SessionDataJson)!;
        state["examQuestionStartedAt"] = DateTime.UtcNow.AddSeconds(-3);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        await service.ValidateActionAsync(student, start.SessionId, new() { Action = "answer_question", QuestionId = question, Answer = "A" });
        var result = await service.CompleteAsync(student, start.SessionId, new());
        Assert.Equal(33, result.TimeSpentSeconds);
        Assert.Equal(30000, result.DetailedResults.GetProperty("examReadingTimeMs").GetInt32());
    }

    private static async Task Configure(OwnedSpeedReadingDbContext db, Guid student, string config)
    {
        var row = await db.Exercises.SingleAsync();
        row.Update(row.Title, row.Description, row.TypeCode, config, 3, row.ExerciseTypeId, null, student, DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private static async Task Read(ISpeedReadingExerciseSessions service, Guid student, Guid session)
    {
        Assert.True((await service.ValidateActionAsync(student, session, new() { Action = "start_reading" })).IsValid);
        Assert.True((await service.ValidateActionAsync(student, session, new() { Action = "finish_reading" })).IsValid);
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions, Guid, Guid, Guid, Guid)> Seed(OwnedSpeedReadingDbContext db, int level)
    {
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "ExamSimulation", "Sınav Simülasyonu", "exam_simulation");
        var exercise = Exercise.Create("Sınav", "exam_simulation", """{"engineConfig":{"timing":{"questionTimeSeconds":60}}}""", 3, student, type.Id);
        var text = ReadingText.Create(Guid.NewGuid(), "Sınav metni", "Okuma yeni düşünceler kazandırır.", difficultyLevel: level);
        var question = Guid.NewGuid();
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.ReadingTexts.Add(text);
        db.ReadingQuestions.Add(ReadingQuestion.Create(question, text.Id, "Metnin ana fikri nedir?", "A", 0, 1, 2, 3,
            optionA: "Okumanın katkısı", optionB: "Ulaşım", optionC: "Spor", optionD: "Hava"));
        await db.SaveChangesAsync();
        var implementation = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        return ((ISpeedReadingExerciseSessions)Activator.CreateInstance(implementation,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!, student, exercise.Id, text.Id, question);
    }
}
