using System.Reflection;
using System.Text.Json.Nodes;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class RsvpSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Requires_matching_text_level(bool pinned)
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        var text = ReadingText.Create(Guid.NewGuid(), "Wrong", "bir iki", difficultyLevel: 1);
        db.ReadingTexts.Add(text);
        await db.SaveChangesAsync();
        var start = () => service.StartAsync(student, new() { ExerciseId = exercise, ReadingTextId = pinned ? text.Id : null });
        await start.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Rotates_text_using_student_history()
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.AddRange(ReadingText.Create(Guid.NewGuid(), "A", "bir iki", difficultyLevel: 3),
            ReadingText.Create(Guid.NewGuid(), "B", "üç dört", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var first = await service.StartAsync(student, new() { ExerciseId = exercise });
        var session = await db.ExerciseSessions.SingleAsync();
        var text = session.ReadingTextId;
        session.Abandon(DateTime.UtcNow);
        await db.SaveChangesAsync();
        var second = await service.StartAsync(student, new() { ExerciseId = exercise });
        (await db.ExerciseSessions.SingleAsync(item => item.Id == second.SessionId)).ReadingTextId.Should().NotBe(text!.Value);
    }

    [Theory]
    [InlineData(false, 1100)]
    [InlineData(true, 2300)]
    public async Task Server_duration_includes_exposure_fixation_and_gaps(bool fixation, int expected)
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db, fixation: fixation);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        started.InitialData.GetProperty("readingMinimumMs").GetInt32().Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Cannot_claim_completion_without_valid_presentation(int scenario)
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört", difficultyLevel: 3));
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        if (scenario == 0)
        {
            var complete = () => service.CompleteAsync(student, started.SessionId, new());
            await complete.Should().ThrowAsync<BusinessRuleException>();
        }
        else
        {
            if (scenario == 2) await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" });
            (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading" })).IsValid.Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stores_verified_presentation_not_reading_speed_and_retries_safely(bool partial)
    {
        await using var db = Context();
        var (service, student, exercise) = await Seed(db);
        var text = ReadingText.Create(Guid.NewGuid(), "A", "bir iki üç dört", difficultyLevel: 3);
        db.ReadingTexts.Add(text);
        db.Entry(text).Property(item => item.WordCount).CurrentValue = 999;
        await db.SaveChangesAsync();
        var started = await service.StartAsync(student, new() { ExerciseId = exercise });
        await service.ValidateActionAsync(student, started.SessionId, new() { Action = "start_reading" });
        var session = await db.ExerciseSessions.SingleAsync();
        var state = JsonNode.Parse(session.SessionDataJson)!;
        state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(partial ? -600 : -1200);
        session.SetState(state.ToJsonString());
        await db.SaveChangesAsync();
        (await service.ValidateActionAsync(student, started.SessionId, new() { Action = "finish_reading", IsTimeout = partial })).IsValid.Should().BeTrue();
        var result = await service.CompleteAsync(student, started.SessionId, new());
        result.RawWPM.Should().BeNull();
        result.Accuracy.Should().BeNull();
        result.WordsRead.Should().BeNull();
        result.XpGained.Should().Be(0);
        result.DetailedResults.GetProperty("rsvpPresentedWords").GetInt32().Should().Be(partial ? 2 : 4);
        result.DetailedResults.GetProperty("rsvpCompletionPercent").GetDecimal().Should().Be(partial ? 50 : 100);
        (await service.CompleteAsync(student, started.SessionId, new())).RawWPM.Should().BeNull();
        db.ExerciseSessionResults.Should().ContainSingle();
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<(ISpeedReadingExerciseSessions, Guid, Guid)> Seed(OwnedSpeedReadingDbContext db, bool fixation = false)
    {
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "RSVP", "RSVP", "text_stream");
        var config = new JsonObject { ["engineConfig"] = new JsonObject {
            ["mode"] = "rsvp", ["timing"] = new JsonObject { ["durationMs"] = 200, ["intervalMs"] = 100 },
            ["visuals"] = new JsonObject { ["showFixation"] = fixation }
        }};
        var exercise = Exercise.Create("RSVP", "reading", config.ToJsonString(), 3, student, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        return ((ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!, student, exercise.Id);
    }
}
