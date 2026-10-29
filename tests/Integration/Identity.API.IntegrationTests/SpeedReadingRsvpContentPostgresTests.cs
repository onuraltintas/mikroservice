using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingRsvpContentPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Prepared_pack_runs_eleven_cells_with_five_verified_answers_and_preserves_snapshots()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        var pack = Path.Combine(directory!.FullName, "content-packs", "rsvp", "v2");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pack, "settings.json")));
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Only the disposable Testcontainers fixture, never a configured application database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var creator = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "RSVP", "Seri Görsel Sunum", "text_stream");
            db.ExerciseTypes.Add(type);
            foreach (var row in settings.RootElement.EnumerateArray().DistinctBy(row => row.GetProperty("ageGroupId").GetGuid()))
            {
                var age = row.GetProperty("age").GetString()!;
                var minimum = age switch { "child" => 9, "teen" => 13, "young-adult" => 17, _ => 22 };
                db.AgeGroupConfigurations.Add(AgeGroupConfiguration.Create(row.GetProperty("ageGroupId").GetGuid(), age, age,
                    minimum, age == "adult" ? null : minimum + (age is "child" or "teen" ? 3 : 4),
                    50, 150, 1000, 70, 15, 1, minimum, true, null, creator, DateTime.UtcNow));
            }
            foreach (var row in settings.RootElement.EnumerateArray())
                db.Exercises.Add(Exercise.Create(row.GetProperty("previousTitle").GetString()!, "RSVP",
                    row.GetProperty("previousConfiguration").GetRawText(), row.GetProperty("level").GetInt32(), creator, type.Id,
                    row.GetProperty("id").GetGuid(), targetAgeGroupId: row.GetProperty("ageGroupId").GetGuid()));
            var legacy = Exercise.Create("Legacy fixture", "RSVP", """
                {"engineType":"text_stream","engineConfig":{"mode":"rsvp","readingPurpose":"practice",
                "timing":{"durationMs":200,"intervalMs":100},"visuals":{"showFixation":true}}}
                """, 3, creator, type.Id);
            db.Exercises.Add(legacy);
            db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Legacy", "bir iki üç dört", difficultyLevel: 3, exerciseId: legacy.Id));
            await db.SaveChangesAsync();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
                "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var old = await service.StartAsync(creator, new() { ExerciseId = legacy.Id });
            var oldSession = await db.ExerciseSessions.SingleAsync(s => s.Id == old.SessionId);
            var oldState = JsonNode.Parse(oldSession.SessionDataJson)!;
            oldState["rsvpProtocolVersion"] = 1; oldState["readingMinimumMs"] = 2300;
            oldSession.SetState(oldState.ToJsonString()); await db.SaveChangesAsync();
            var snapshot = oldSession.SessionDataJson;
            await using (var connection = new NpgsqlConnection(postgres.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(pack, "apply.sql")), connection);
                await command.ExecuteNonQueryAsync(); await command.ExecuteNonQueryAsync();
            }
            db.ChangeTracker.Clear();
            Assert.Equal(22, await db.ReadingTexts.CountAsync(t => t.Category == "rsvp"));
            Assert.Equal(110, await db.ReadingQuestions.CountAsync());
            Assert.True(JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(snapshot),
                JsonSerializer.Deserialize<JsonElement>((await db.ExerciseSessions.SingleAsync(s => s.Id == old.SessionId)).SessionDataJson)));
            foreach (var row in settings.RootElement.EnumerateArray())
            {
                var user = Guid.NewGuid(); var exerciseId = row.GetProperty("id").GetGuid();
                db.UserProfiles.Add(SpeedReadingUserProfile.Import(Guid.NewGuid(), user, row.GetProperty("level").GetInt32(),
                    200, 70, 15, row.GetProperty("ageGroupId").GetGuid(), null, true, DateTime.UtcNow, null, null, null));
                await db.SaveChangesAsync();
                var started = await service.StartAsync(user, new() { ExerciseId = exerciseId });
                Assert.Equal(2, started.InitialData.GetProperty("rsvpProtocolVersion").GetInt32());
                Assert.Equal(5, started.InitialData.GetProperty("questions").GetArrayLength());
                Assert.DoesNotContain("correctAnswer", started.InitialData.GetRawText());
                var session = await db.ExerciseSessions.SingleAsync(s => s.Id == started.SessionId);
                var text = await db.ReadingTexts.SingleAsync(t => t.Id == session.ReadingTextId);
                Assert.Equal(row.GetProperty("ageGroupId").GetGuid(), text.TargetAgeGroupId);
                Assert.Equal(row.GetProperty("level").GetInt32(), text.DifficultyLevel);
                var config = row.GetProperty("configuration").GetProperty("engineConfig");
                var band = config.GetProperty("content");
                Assert.InRange(text.WordCount, band.GetProperty("minWordCount").GetInt32(), band.GetProperty("maxWordCount").GetInt32());
                var minimumMs = 300 + text.WordCount * config.GetProperty("timing").GetProperty("durationMs").GetInt32();
                Assert.Equal(minimumMs, started.InitialData.GetProperty("readingMinimumMs").GetInt32());
                await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.NotFoundException>(() => service.ValidateActionAsync(Guid.NewGuid(), session.Id, new() { Action = "start_reading" }));
                Assert.True((await service.ValidateActionAsync(user, session.Id, new() { Action = "start_reading" })).IsValid);
                Assert.False((await service.ValidateActionAsync(user, session.Id, new() { Action = "finish_reading" })).IsValid);
                await service.PauseAsync(user, session.Id); await service.ResumeAsync(user, session.Id);
                var state = JsonNode.Parse(session.SessionDataJson)!;
                state["readingStartTime"] = DateTime.UtcNow.AddMilliseconds(-minimumMs - 1000);
                session.SetState(state.ToJsonString()); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                Assert.True((await service.ValidateActionAsync(user, session.Id, new() { Action = "finish_reading" })).IsValid);
                foreach (var question in await db.ReadingQuestions.Where(q => q.ReadingTextId == text.Id).OrderBy(q => q.OrderIndex).ToListAsync())
                    Assert.True((await service.ValidateActionAsync(user, session.Id,
                        new() { Action = "answer_question", QuestionId = question.Id, Answer = question.CorrectAnswer })).IsValid);
                var result = await service.CompleteAsync(user, session.Id, new());
                var replay = await service.CompleteAsync(user, session.Id, new());
                Assert.Equal(100m, result.ComprehensionScore); Assert.Equal(result.ComprehensionScore, replay.ComprehensionScore);
                Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
                Assert.Equal(text.WordCount, result.DetailedResults.GetProperty("rsvpPresentedWords").GetInt32());
                Assert.Equal(100m, result.DetailedResults.GetProperty("rsvpCompletionPercent").GetDecimal());
                var next = await service.StartAsync(user, new() { ExerciseId = exerciseId });
                Assert.NotEqual(text.Id, (await db.ExerciseSessions.SingleAsync(s => s.Id == next.SessionId)).ReadingTextId);
            }
            Assert.Equal(11, await db.ExerciseSessionResults.CountAsync());
            var resumed = await service.StartAsync(creator, new() { ExerciseId = legacy.Id, ReadingTextId = oldSession.ReadingTextId });
            Assert.Equal(old.SessionId, resumed.SessionId);
            Assert.Equal(1, resumed.InitialData.GetProperty("rsvpProtocolVersion").GetInt32());
            Assert.Equal(2300, resumed.InitialData.GetProperty("readingMinimumMs").GetInt32());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
