using System.Reflection;
using System.Text.Json.Nodes;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SkimmingSessionTests
{
    [Fact]
    public async Task Uses_main_idea_questions_not_keyword_targets_or_catalog_text()
    {
        await using var db = Context();
        var (service, student, exercise, text, question) = await Seed(db);
        var start = await service.StartAsync(student, new() { ExerciseId = exercise });
        start.InitialData.GetProperty("content").GetString().Should().Be("Kitaplar öğrenmeyi destekler. Okuma yeni düşünceler kazandırır.");
        start.InitialData.GetProperty("questions").GetArrayLength().Should().Be(1);
        start.InitialData.GetProperty("questions")[0].GetProperty("questionId").GetGuid().Should().Be(question);
        start.InitialData.GetProperty("questions")[0].TryGetProperty("correctAnswer", out _).Should().BeFalse();
        start.InitialData.GetProperty("readingTextId").GetGuid().Should().Be(text);
        start.InitialData.GetProperty("skimmingProtocolVersion").GetInt32().Should().Be(1);
        (await service.ValidateActionAsync(student, start.SessionId, new() { Action = "scan_click", Index = 0 })).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_wrong_level_even_for_explicit_text(bool explicitText)
    {
        await using var db = Context();
        var (service, student, exercise, text, _) = await Seed(db, level: 1);
        var start = () => service.StartAsync(student, new() { ExerciseId = exercise, ReadingTextId = explicitText ? text : null });
        await start.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Requires_a_scorable_main_idea_question(int questionType)
    {
        await using var db = Context();
        var (service, student, exercise, _, _) = await Seed(db, questionType: questionType);
        await ((Func<Task>)(async () => await service.StartAsync(student, new() { ExerciseId = exercise })))
            .Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Cannot_finish_or_answer_before_verified_inspection()
    {
        await using var db = Context();
        var (service, student, exercise, _, question) = await Seed(db);
        var start = await service.StartAsync(student, new() { ExerciseId = exercise });
        (await service.ValidateActionAsync(student, start.SessionId, new() { Action = "finish_reading" })).IsValid.Should().BeFalse();
        (await service.ValidateActionAsync(student, start.SessionId, new() { Action = "answer_question", QuestionId = question, Answer = "A" })).IsValid.Should().BeFalse();
        await service.ValidateActionAsync(student, start.SessionId, new() { Action = "start_reading" });
        (await service.ValidateActionAsync(student, start.SessionId, new() { Action = "finish_reading", IsTimeout = true })).IsValid.Should().BeFalse();
        (await service.ValidateActionAsync(student, start.SessionId, new() { Action = "finish_reading" })).IsValid.Should().BeFalse();
        await ((Func<Task>)(async () => await service.CompleteAsync(student, start.SessionId, new())))
            .Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Rejects_question_metadata_that_would_fail_result_persistence()
    {
        await using var db = Context();
        var (service, student, exercise, _, question) = await Seed(db);
        db.Entry(await db.ReadingQuestions.SingleAsync(item => item.Id == question)).Property(item => item.BloomLevel).CurrentValue = 0;
        await db.SaveChangesAsync();
        await ((Func<Task>)(async () => await service.StartAsync(student, new() { ExerciseId = exercise })))
            .Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Legacy_search_session_is_retained_but_replaced_with_inspection_protocol()
    {
        await using var db = Context();
        var (service, student, exercise, _, _) = await Seed(db);
        var first = await service.StartAsync(student, new() { ExerciseId = exercise });
        var old = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(old.SessionDataJson)!; state.AsObject().Remove("skimmingProtocolVersion");
        old.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        var second = await service.StartAsync(student, new() { ExerciseId = exercise });
        second.SessionId.Should().NotBe(first.SessionId);
        (await db.ExerciseSessions.SingleAsync(item => item.Id == first.SessionId)).Status
            .Should().Be(SpeedReading.Domain.Sessions.ExerciseSessionStatus.Abandoned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Records_inspection_and_main_idea_accuracy_without_reading_wpm(bool timedOut)
    {
        await using var db = Context();
        var (service, student, exercise, _, question) = await Seed(db);
        var start = await service.StartAsync(student, new() { ExerciseId = exercise });
        await service.ValidateActionAsync(student, start.SessionId, new() { Action = "start_reading" });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddSeconds(timedOut ? -91 : -6);
        session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
        (await service.ValidateActionAsync(student, start.SessionId, new() { Action = "finish_reading", IsTimeout = timedOut })).IsValid.Should().BeTrue();
        (await service.ValidateActionAsync(student, start.SessionId, new() { Action = "answer_question", QuestionId = question, Answer = "A" })).IsValid.Should().BeTrue();
        var result = await service.CompleteAsync(student, start.SessionId, new());
        result.RawWPM.Should().BeNull(); result.WordsRead.Should().BeNull(); result.WeightedKDP.Should().BeNull();
        result.ComprehensionScore.Should().Be(100);
        result.DetailedResults.GetProperty("skimmingInspectionMs").GetInt32().Should().BeInRange(timedOut ? 90_000 : 6000, timedOut ? 90_000 : 7000);
        result.DetailedResults.GetProperty("readingIncomplete").GetBoolean().Should().Be(timedOut);
        if (timedOut) result.XpGained.Should().Be(0);
        (await service.CompleteAsync(student, start.SessionId, new())).WordsRead.Should().BeNull();
        db.ExerciseSessionResults.Should().ContainSingle();
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<(ISpeedReadingExerciseSessions Service, Guid Student, Guid Exercise, Guid Text, Guid Question)> Seed(
        OwnedSpeedReadingDbContext db, int level = 3, int questionType = 1)
    {
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "Skimming", "Göz Gezdirme", "skimming");
        var exercise = Exercise.Create("Göz Gezdirme", "reading", """
            {"engineConfig":{"timeLimitSeconds":90,"timing":{"minReadingTimeMs":5000},"content":{"text":"Katalogdaki eski metin"},"targets":{"words":["eski"]}}}
            """, 3, student, type.Id);
        var text = ReadingText.Create(Guid.NewGuid(), "Okuma", "Kitaplar öğrenmeyi destekler. Okuma yeni düşünceler kazandırır.", difficultyLevel: level);
        var question = Guid.NewGuid();
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.ReadingTexts.Add(text);
        db.ReadingQuestions.Add(ReadingQuestion.Create(question, text.Id, "Metnin ana fikri nedir?", "A", 0, questionType, 2, 3,
            optionA: "Okumanın katkısı", optionB: "Ulaşım", optionC: "Spor", optionD: "Hava"));
        db.ReadingQuestions.Add(ReadingQuestion.Create(Guid.NewGuid(), text.Id, "Ayrıntı", "B", 1, 3,
            optionA: "A", optionB: "B", optionC: "C", optionD: "D"));
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        return ((ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!, student, exercise.Id, text.Id, question);
    }
}
