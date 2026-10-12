using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Application.UnitTests;

public sealed class AssessmentBlockProtocolTests
{
    [Theory]
    [InlineData("null", false)]
    [InlineData("[1,500,5000]", true)]
    [InlineData("[18,13,8]", true)]
    [InlineData("[200,500,800]", false)]
    [InlineData("[800,500,1]", false)]
    public void Invalid_block_values_are_rejected(string values, bool visual)
    {
        using var config = JsonDocument.Parse("{\"assessmentProtocol\":{\"version\":2,\"practiceRounds\":3,\"roundsPerBlock\":6,\"blockValues\":" + values + "}}");
        Assert.Throws<ArgumentException>(() => AssessmentBlockProtocol.Parse(config.RootElement, visual));
    }
    [Theory]
    [InlineData(1, "Tachistoscope", 6, 1000, 750, 500)]
    [InlineData(2, "Tachistoscope", 8, 800, 600, 400)]
    [InlineData(4, "Tachistoscope", 10, 750, 500, 300)]
    [InlineData(3, "Tachistoscope", 10, 750, 500, 300)]
    [InlineData(1, "VisualExpansion", 6, 8, 12, 16)]
    [InlineData(2, "VisualExpansion", 8, 8, 13, 18)]
    [InlineData(4, "VisualExpansion", 10, 8, 13, 18)]
    [InlineData(3, "VisualExpansion", 10, 8, 13, 18)]
    public async Task Fixed_blocks_exclude_practice_and_resume_without_adaptation(
        int ageIndex, string typeName, int perBlock, int first, int middle, int last)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var actor = Guid.NewGuid();
        var tach = typeName == "Tachistoscope";
        var total = 3 + perBlock * 3;
        var config = JsonSerializer.Serialize(new {
            engineType = tach ? "text_stream" : "visual_expansion",
            engineConfig = new {
                mode = tach ? "tachistoscope" : "horizontal", rounds = total,
                content = new { type = "number", count = total, length = 3, stimulusType = "letter" },
                timing = new { durationMs = tach ? first : 200 }, displayDurationMs = tach ? first : 200,
                startDegrees = 8, targetDegrees = 18, adaptive = new { enabled = false },
                assessmentProtocol = new { version = 2, practiceRounds = 3, roundsPerBlock = perBlock,
                    blockValues = new[] { first, middle, last } }
            }
        });
        var type = ExerciseType.Create(Guid.NewGuid(), typeName, "Test", tach ? "text_stream" : "visual_expansion");
        var age = Guid.Parse($"10000000-0000-0000-0000-00000000000{ageIndex}");
        var exercise = Exercise.Create("Assessment", "Test", config, 1, actor, type.Id, targetAgeGroupId: age);
        var attempt = AssessmentAttempt.Start(Guid.NewGuid(), actor, AssessmentAttemptPhase.Baseline,
            "tr-baseline-v2", "tr", null, 1, DateTime.UtcNow, actor.ToString());
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.AssessmentAttempts.Add(attempt);
        db.AssessmentAttemptExercises.Add(AssessmentAttemptExercise.Pin(Guid.NewGuid(), attempt.Id, exercise.Id,
            null, tach ? "tachistoscope" : "visual", 1, JsonSerializer.Serialize(new {
                version = 1, exercise = new { id = exercise.Id, title = exercise.Title, description = "Test",
                    typeName, difficultyLevel = 1, engineType = tach ? "text_stream" : "visual_expansion", configurationJson = config },
                questions = Array.Empty<object>()
            }), DateTime.UtcNow, actor.ToString()));
        await db.SaveChangesAsync();
        var owned = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(owned,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        var request = new StartExerciseSessionRequest { ExerciseId = exercise.Id, AssessmentAttemptId = attempt.Id };
        var started = await service.StartAsync(actor, request);
        Assert.Equal(total, started.TotalSteps);
        var seen = new HashSet<string>();
        for (var round = 0; round < total; round++)
        {
            var presented = await service.ValidateActionAsync(actor, started.SessionId,
                new() { Action = tach ? "tachistoscope_present" : "visual_expansion_present", Index = round });
            Assert.True(presented.IsValid);
            var feedback = presented.FeedbackData!.Value;
            var expectedValue = new[] { first, middle, last }[Math.Clamp((round - 3) / perBlock, 0, 2)];
            Assert.Equal(tach ? expectedValue : 200, feedback.GetProperty("displayDurationMs").GetInt32());
            if (!tach) Assert.Equal(expectedValue, feedback.GetProperty("degrees").GetInt32());
            Assert.Equal(round < 3, feedback.GetProperty("isPractice").GetBoolean());
            var stimulus = tach ? feedback.GetProperty("stimulus").GetString()! : "";
            if (tach) { Assert.Equal(3, stimulus.Length); Assert.True(seen.Add(stimulus)); }
            var session = await db.ExerciseSessions.SingleAsync();
            var state = JsonNode.Parse(session.SessionDataJson)!;
            if (tach) state["tachistoscope"]!["presentedAt"] = DateTime.UtcNow.AddSeconds(-2);
            else state["visualExpansionPresentedAt"] = DateTime.UtcNow.AddSeconds(-2);
            session.SetState(state.ToJsonString()); await db.SaveChangesAsync();
            var answered = await service.ValidateActionAsync(actor, started.SessionId, new() {
                Action = tach ? "tachistoscope_answer" : "visual_expansion_answer", Index = round,
                Answer = round < 3 ? "wrong" : stimulus,
                Answers = round < 3 ? ["wrong", "wrong"] : tach ? [] : feedback.GetProperty("stimuli").EnumerateArray().Select(x => x.GetString()!).ToList()
            });
            Assert.True(answered.IsValid); Assert.Null(answered.IsCorrect);
            Assert.Equal(Math.Max(0, round - 2), session.CorrectCount);
            Assert.Equal(0, session.IncorrectCount);
            if (round == 4)
            {
                await service.PauseAsync(actor, started.SessionId); db.ChangeTracker.Clear();
                await service.ResumeAsync(actor, started.SessionId);
                var resumed = await service.StartAsync(actor, request);
                Assert.Equal(started.SessionId, resumed.SessionId);
                Assert.Equal(total, resumed.TotalSteps);
            }
        }
        var completed = await service.CompleteAsync(actor, started.SessionId, new() { IsAssessmentMode = true });
        Assert.Equal(100, completed.Accuracy);
        if (tach) Assert.Equal(0, completed.DetailedResults.GetProperty("tachistoscope").GetProperty("trials").GetArrayLength());
        else Assert.False(completed.DetailedResults.TryGetProperty("visualExpansionRoundResults", out _));
        Assert.Equal(3, completed.DetailedResults.GetProperty("assessmentBlockResults").GetArrayLength());
        foreach (var block in completed.DetailedResults.GetProperty("assessmentBlockResults").EnumerateArray())
        {
            Assert.Equal(perBlock, block.GetProperty("rounds").GetInt32());
            Assert.Equal(100, block.GetProperty("accuracy").GetDecimal());
        }
    }
}
