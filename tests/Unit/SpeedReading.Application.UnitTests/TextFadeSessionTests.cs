using System.Reflection;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class TextFadeSessionTests
{
    [Fact]
    public async Task Verifies_pace_and_lag_before_completion_without_fabricated_metrics()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        started.InitialData.GetProperty("readingMinimumMs").GetInt32().Should().Be(2200);
        var complete = () => service.CompleteAsync(student, started.SessionId, new());
        await complete.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" })).IsValid.Should().BeTrue();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid.Should().BeFalse();
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(-2300);
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid.Should().BeTrue();
        var result = await service.CompleteAsync(student, started.SessionId, new());
        result.RawWPM.Should().BeNull();
        result.Accuracy.Should().BeNull();
        result.XpGained.Should().Be(0);
        result.DetailedResults.GetProperty("fadeDisplayPaceWpm").GetDecimal().Should().Be(200);
        result.DetailedResults.GetProperty("fadeCompletionPercent").GetDecimal().Should().Be(100);
        await service.CompleteAsync(student, started.SessionId, new());
        db.ExerciseSessionResults.Should().ContainSingle();
    }

    [Fact]
    public async Task Deadline_before_minimum_forces_partial_even_when_client_claims_completion()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db,
            """{"engineType":"text_fade","timing":{"timeLimitSec":1},"fading":{"speedWpm":200,"lagMs":300},"engineConfig":{"lagMs":0}}}""");
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört beş altı", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        started.InitialData.GetProperty("readingMinimumMs").GetInt32().Should().Be(1800);
        await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(-1100);
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid.Should().BeTrue();
        var result = await service.CompleteAsync(student, started.SessionId, new());
        result.DetailedResults.GetProperty("readingIncomplete").GetBoolean().Should().BeTrue();
        result.DetailedResults.GetProperty("fadeCompletionPercent").GetDecimal().Should().BeLessThan(100);
    }

    [Fact]
    public async Task Rejects_direct_legacy_completion_and_restarts_the_old_session()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state.AsObject().Remove("fadeDisplayPaceWpm");
        state["readingMinimumMs"] = 0;
        state["readingStartTime"] = DateTime.UtcNow.AddSeconds(-2);
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid.Should().BeFalse();
        state["readingEndTime"] = DateTime.UtcNow;
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        var complete = () => service.CompleteAsync(student, started.SessionId, new());
        await complete.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>();
        (await service.StartAsync(student, new() { ExerciseId = exercise })).SessionId.Should().NotBe(started.SessionId);
    }

    [Theory]
    [InlineData("""{"engineType":"text_fade","timing":{"timeLimitSec":-1}}""")]
    [InlineData("""{"engineType":"text_fade","engineConfig":{"timing":{"timeLimitSec":3601}}}""")]
    public void Rejects_invalid_deadline_configuration(string config)
    {
        var validate = () => ExerciseConfigurationRules.ValidateActiveConfiguration(config, "text_fade");
        validate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Rejects_explicit_wrong_level_text()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        var text = ReadingText.Create(Guid.NewGuid(), "Yanlış", "bir iki", difficultyLevel: 1);
        db.ReadingTexts.Add(text);
        await db.SaveChangesAsync();
        var start = () => service.StartAsync(student, new() { ExerciseId = exercise, ReadingTextId = text.Id });
        await start.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Requires_matching_active_text_instead_of_another_level_or_sample()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Yanlış", "bir iki", difficultyLevel: 1));
        await db.SaveChangesAsync();
        var start = () => service.StartAsync(student, new() { ExerciseId = exercise });
        await start.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Rotates_matching_text_using_only_this_students_history()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.Parse("10000000-0000-0000-0000-000000000001"), "A", "bir iki üç dört", difficultyLevel: 3));
        db.ReadingTexts.Add(ReadingText.Create(Guid.Parse("10000000-0000-0000-0000-000000000002"), "B", "beş altı yedi sekiz", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var first = await service.StartAsync(student, new() { ExerciseId = exercise });
        var session = await db.ExerciseSessions.SingleAsync();
        var selected = session.ReadingTextId;
        session.Abandon(DateTime.UtcNow);
        await db.SaveChangesAsync();
        await service.StartAsync(student, new() { ExerciseId = exercise });
        (await db.ExerciseSessions.SingleAsync(item => item.Id != first.SessionId)).ReadingTextId.Should().NotBe(selected!.Value);
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions Service, Guid Student, Guid Exercise)> Seed(OwnedSpeedReadingDbContext db, string? config = null)
    {
        var student = Guid.NewGuid();
        var type = Guid.NewGuid();
        var exercise = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(type, "TextFading", "Metin Solma", "text_fade"));
        db.Exercises.Add(Exercise.Create("Metin Solma", "reading",
            config ?? """{"engineType":"text_fade","engineConfig":{"fading":{"speedWpm":200,"lagMs":1000}}}""",
            3, student, type, id: exercise));
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        return ((ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!, student, exercise);
    }
}
