using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ErrorAnalysisPreparedContentTests
{
    [Fact]
    public async Task Prepared_errors_replace_only_annotated_tokens_and_hide_original_text()
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var (exercise, text) = await Seed(db, user);
        var started = await Service(db).StartAsync(user, new() { ExerciseId = exercise.Id });
        var words = started.InitialData.GetProperty("errorAnalysisWords");
        Assert.Equal("yanlız", words[0].GetProperty("text").GetString());
        Assert.Equal("bugün.", words[1].GetProperty("text").GetString());
        Assert.Equal(1, started.TotalSteps);
        Assert.DoesNotContain("yalnız", started.InitialData.GetRawText());
        await Service(db).ValidateActionAsync(user, started.SessionId, new() { Action = "error_analysis_start" });
        await Service(db).ValidateActionAsync(user, started.SessionId, new() { Action = "error_analysis_select", Index = 0 });
        var result = await Service(db).CompleteAsync(user, started.SessionId, new());
        Assert.Equal(100m, result.Score); Assert.Null(result.RawWPM); Assert.Null(result.ComprehensionScore);
        Assert.Equal(text.Id, (await db.ExerciseSessions.SingleAsync()).ReadingTextId);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("count")]
    [InlineData("original")]
    [InlineData("duplicate")]
    [InlineData("index")]
    public async Task Invalid_annotations_cannot_silently_generate_or_reduce_errors(string reason)
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var (exercise, _) = await Seed(db, user);
        var config = JsonNode.Parse(exercise.ConfigurationJson)!;
        var settings = config["engineConfig"]!;
        var map = settings["errorsByTextId"]!.AsObject();
        var errors = map.First().Value!.AsArray();
        if (reason == "missing") settings.AsObject().Remove("errorsByTextId");
        if (reason == "count") settings["errorCount"] = 2;
        if (reason == "original") errors[0]!["originalWord"] = "başka";
        if (reason == "duplicate") errors.Add(errors[0]!.DeepClone());
        if (reason == "index") errors[0]!["wordIndex"] = 9;
        exercise.Update(exercise.Title, exercise.Description, exercise.TypeCode, config.ToJsonString(), 4,
            exercise.ExerciseTypeId, exercise.TargetAgeGroupId, user, DateTime.UtcNow);
        await db.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => Service(db).StartAsync(user, new() { ExerciseId = exercise.Id }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Theory]
    [InlineData(3, 2, false)]
    [InlineData(4, 3, false)]
    [InlineData(4, 2, true)]
    public async Task Explicit_text_cannot_bypass_level_length_or_age(int level, int length, bool commonAge)
    {
        await using var db = Context(); var user = Guid.NewGuid();
        var (exercise, _) = await Seed(db, user);
        var text = ReadingText.Create(Guid.NewGuid(), "Başka", string.Join(" ", Enumerable.Repeat("kelime", length)),
            difficultyLevel: level, exerciseId: exercise.Id, targetAgeGroupId: commonAge ? null : exercise.TargetAgeGroupId);
        db.ReadingTexts.Add(text); await db.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => Service(db).StartAsync(user, new() { ExerciseId = exercise.Id, ReadingTextId = text.Id }));
        Assert.Empty(db.ExerciseSessions);
    }

    [Fact]
    public void Student_configuration_redacts_prepared_error_map_in_all_scopes()
    {
        var config = JsonSerializer.Serialize(new { engineType = "error_analysis", engineConfig = new {
            errorsByTextId = new Dictionary<string, object> { [Guid.NewGuid().ToString()] = new[] {
                new { wordIndex = 0, originalWord = "yalnız", errorWord = "yanlız" }
            } }
        } });
        Assert.DoesNotContain("yalnız", SpeedReadingContentSecurity.SanitizeExerciseConfiguration(config));
        Assert.DoesNotContain("errorsByTextId", SpeedReadingContentSecurity.SanitizeExerciseConfiguration(config));
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) => (ISpeedReadingExerciseSessions)Activator.CreateInstance(
        typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
    private static async Task<(Exercise, ReadingText)> Seed(OwnedSpeedReadingDbContext db, Guid user)
    {
        var age = Guid.NewGuid(); var textId = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "ErrorAnalysis", "Hata Analizi", "error_analysis");
        var config = JsonSerializer.Serialize(new { engineType = "error_analysis", engineConfig = new {
            content = new { source = "reading_text", minWordCount = 2, maxWordCount = 2 }, errorCount = 1, timeLimit = 240,
            errorsByTextId = new Dictionary<string, object> { [textId.ToString()] = new[] {
                new { wordIndex = 0, originalWord = "yalnız", errorWord = "yanlız", errorType = "transposition", explanation = "Doğru yazım yalnız." }
            } }
        } });
        var exercise = Exercise.Create("Hata Analizi", "error_analysis", config, 4, user, type.Id, targetAgeGroupId: age);
        var text = ReadingText.Create(textId, "Metin", "yalnız bugün.", difficultyLevel: 4, exerciseId: exercise.Id, targetAgeGroupId: age);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.ReadingTexts.Add(text); await db.SaveChangesAsync();
        return (exercise, text);
    }
}
