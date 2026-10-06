using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ScanningSessionTests
{
    [Fact]
    public async Task Clicks_are_validated_deduplicated_and_saved_without_reading_metrics()
    {
        await using var db = Context();
        var (service, student, started) = await Start(db);
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_click", Index = 0, Number = 0 })).IsValid.Should().BeFalse();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_start" })).IsValid.Should().BeTrue();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "advance" })).IsValid.Should().BeFalse();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_click", Index = 99, Number = 0 })).IsValid.Should().BeFalse();
        var wrong = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_click", Index = 2, Number = 0 });
        wrong.IsValid.Should().BeTrue();
        wrong.IsCorrect.Should().BeFalse();
        var id = Guid.NewGuid();
        var click = new ExerciseActionRequest { Action = "scan_click", Index = 0, Number = 0, ActionId = id };
        (await service.ValidateActionAsync(student, started.SessionId, click)).IsCorrect.Should().BeTrue();
        await service.ValidateActionAsync(student, started.SessionId, click);
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_click", Index = 0, Number = 0 })).IsValid.Should().BeFalse();
        var early = () => service.CompleteAsync(student, started.SessionId, new());
        await early.Should().ThrowAsync<BusinessRuleException>();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_click", Index = 1, Number = 0 })).IsCompleted.Should().BeTrue();
        var result = await service.CompleteAsync(student, started.SessionId, new());
        result.CorrectCount.Should().Be(2);
        result.IncorrectCount.Should().Be(1);
        result.RawWPM.Should().BeNull();
        result.WordsRead.Should().BeNull();
        db.ReadingSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Deadline_keeps_partial_results_without_awarding_xp()
    {
        await using var db = Context();
        var (service, student, started) = await Start(db);
        await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_start" });
        await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_click", Index = 0, Number = 0 });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-20);
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        var finished = await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_timeout" });
        finished.IsCompleted.Should().BeTrue();
        var result = await service.CompleteAsync(student, started.SessionId, new());
        result.CorrectCount.Should().Be(1);
        result.XpGained.Should().Be(0);
        result.Accuracy.Should().Be(50);
        (await service.CompleteAsync(student, started.SessionId, new())).XpGained.Should().Be(0);
    }

    [Fact]
    public async Task Generates_real_targets_when_catalog_only_defines_target_count()
    {
        await using var db = Context();
        var (_, _, started) = await Start(db, """{"engineConfig":{"targetCount":2}}""");
        started.TotalSteps.Should().Be(2);
        started.InitialData.GetProperty("scanningRounds")[0].GetProperty("targets").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Rejects_targets_that_do_not_exist_in_the_text()
    {
        await using var db = Context();
        var start = () => Start(db, """{"engineConfig":{"targets":{"words":["olmayan"]}}}""");
        await start.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Does_not_fall_back_to_a_different_difficulty()
    {
        await using var db = Context();
        var start = () => Start(db, textLevel: 1);
        await start.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Skimming_alias_uses_the_same_validated_protocol()
    {
        await using var db = Context();
        var (service, student, started) = await Start(db, engine: "skimming");
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "scan_start" })).IsValid.Should().BeTrue();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "advance" })).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Legacy_unverified_session_is_preserved_and_replaced_on_start()
    {
        await using var db = Context();
        var (service, student, started) = await Start(db);
        var old = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(old.SessionDataJson)!;
        state.AsObject().Remove("scanningRounds");
        old.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        var fresh = await service.StartAsync(student, new() { ExerciseId = started.ExerciseId });
        fresh.SessionId.Should().NotBe(started.SessionId);
        (await db.ExerciseSessions.SingleAsync(item => item.Id == old.Id)).Status.Should().Be(SpeedReading.Domain.Sessions.ExerciseSessionStatus.Abandoned);
        fresh.TotalSteps.Should().Be(2);
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ISpeedReadingExerciseSessions Service, Guid Student, StartExerciseSessionResponse Started)> Start(
        OwnedSpeedReadingDbContext db, string? config = null, int textLevel = 3, string engine = "scanning")
    {
        var student = Guid.NewGuid();
        var type = Guid.NewGuid();
        var exercise = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(type, "Scanning", "Tarama", engine));
        db.Exercises.Add(Exercise.Create("Tarama", "reading", config ?? """{"engineConfig":{"targets":{"words":["ışık","inci"]},"timeLimit":10}}""", 3, student, type, id: exercise));
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Metin", "“IŞIK” [İNCİ] kelime", difficultyLevel: textLevel));
        await db.SaveChangesAsync();
        var ownedType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(ownedType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        return (service, student, await service.StartAsync(student, new() { ExerciseId = exercise }));
    }
}
