using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Visualization;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Visualization;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingVisualizationPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_admin_updates_leave_only_one_active_question_set(bool retries)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync(); var actor = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
            var exercise = Exercise.Create("Görselleştirme", "strategy", "{}", 3, actor, type.Id);
            var scene = VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Eski sahne", null, 30, 0, 3, null, actor, DateTime.UtcNow);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.VisualizationScenes.Add(scene);
            db.VisualizationQuestions.Add(VisualizationQuestion.Create(Guid.NewGuid(), scene.Id, "Eski soru", "[\"Bir\",\"İki\"]", "A", "detail", 0, null, actor, DateTime.UtcNow));
            await db.SaveChangesAsync();
            var gate = new QuestionReadGate();
            async Task Update(string label)
            {
                await using var writer = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                    .UseNpgsql(postgres.ConnectionString, options => { if (retries) options.EnableRetryOnFailure(); })
                    .AddInterceptors(gate).Options);
                var service = (ISpeedReadingVisualization)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
                    .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingVisualization")!,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [writer], null)!;
                Assert.True(await service.UpdateSceneAsync(scene.Id, new(exercise.Id, label, null, 30, 0, 3,
                    Enumerable.Range(0, 3).Select(index => new VisualizationQuestionRequest($"{label} soru {index}",
                        ["Bir", "İki"], "A", "detail", index)).ToArray(), Mode: "assessment"),
                    actor, $"update-visualization-{label}", CancellationToken.None));
            }
            await Task.WhenAll(Update("first"), Update("second"));
            var active = await db.VisualizationQuestions.AsNoTracking().Where(question => question.SceneId == scene.Id && !question.IsDeleted).ToListAsync();
            Assert.Equal(3, active.Count); Assert.Equal(3, active.Select(question => question.DisplayOrder).Distinct().Count());
            Assert.Single(active.Select(question => question.QuestionText.Split(' ')[0]).Distinct());
            await using var deleter = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql(postgres.ConnectionString, options => { if (retries) options.EnableRetryOnFailure(); }).Options);
            var deletion = (ISpeedReadingVisualization)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
                .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingVisualization")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [deleter], null)!;
            Assert.True(await deletion.DeleteSceneAsync(scene.Id, actor, "delete-visualization-scene", CancellationToken.None));
            Assert.True(await deletion.DeleteSceneAsync(scene.Id, actor, "delete-visualization-scene", CancellationToken.None));
            Assert.False(await db.VisualizationQuestions.AsNoTracking().AnyAsync(question => question.SceneId == scene.Id && !question.IsDeleted));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class QuestionReadGate : DbCommandInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM speed_reading.visualization_questions", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
                // Forces both pre-fix writers to read the old set. A serialized writer is allowed to finish.
                await Task.WhenAny(ready.Task, Task.Delay(1000, cancellationToken));
            }
            return result;
        }
    }

    [Theory]
    [InlineData("none")]
    [InlineData("description")]
    [InlineData("hint")]
    [InlineData("extra-question")]
    [InlineData("duplicate-baseline")]
    [InlineData("duplicate-question")]
    [InlineData("null-options")]
    public async Task V2_pack_preserves_old_sessions_rotates_new_scenes_and_rejects_edited_baseline(string change)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var student = Guid.NewGuid(); var baseline = ReadPack("baseline.json");
            using var document = JsonDocument.Parse(baseline);
            var oldScenes = document.RootElement.EnumerateArray().ToArray();
            var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
            db.ExerciseTypes.Add(type);
            foreach (var group in oldScenes.GroupBy(scene => scene.GetProperty("exerciseId").GetGuid()))
            {
                var scene = group.First();
                db.Exercises.Add(Exercise.Create("Görselleştirme", "strategy", "{}", scene.GetProperty("difficultyLevel").GetInt32(),
                    student, type.Id, group.Key, targetAgeGroupId: scene.GetProperty("targetAgeGroupId").GetGuid()));
            }
            foreach (var scene in oldScenes)
            {
                var id = scene.GetProperty("id").GetGuid();
                db.VisualizationScenes.Add(VisualizationScene.Create(id, scene.GetProperty("exerciseId").GetGuid(),
                    scene.GetProperty("description").GetString()!, null, scene.GetProperty("duration").GetInt32(),
                    scene.GetProperty("displayOrder").GetInt32(), scene.GetProperty("difficultyLevel").GetInt32(),
                    scene.GetProperty("targetAgeGroupId").GetGuid(), student, DateTime.UtcNow, "assessment"));
                foreach (var q in scene.GetProperty("questions").EnumerateArray())
                    db.VisualizationQuestions.Add(VisualizationQuestion.Create(q.GetProperty("id").GetGuid(), id,
                        q.GetProperty("questionText").GetString()!, q.GetProperty("options").GetRawText(),
                        q.GetProperty("correctAnswer").GetString()!, q.GetProperty("questionType").GetString()!,
                        q.GetProperty("displayOrder").GetInt32(), null, student, DateTime.UtcNow));
            }
            var profile = SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), student, DateTime.UtcNow);
            var ageId = oldScenes[0].GetProperty("targetAgeGroupId").GetGuid();
            db.AgeGroupConfigurations.Add(AgeGroupConfiguration.Create(ageId, "child", "Çocuk", 9, 12,
                100, 250, 500, 75, 20, 1, 0, true, null, student, DateTime.UtcNow));
            profile.UpdateSettings(1, 250, 75, 20, ageId, student, DateTime.UtcNow);
            db.UserProfiles.Add(profile); await db.SaveChangesAsync();
            var exerciseId = oldScenes[0].GetProperty("exerciseId").GetGuid();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
                .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var previous = await service.StartAsync(student, new() { ExerciseId = exerciseId });
            var snapshot = (await db.ExerciseSessions.SingleAsync()).SessionDataJson;
            if (change != "none")
            {
                var sql = ReadPack("apply.sql");
                if (change == "description")
                    await db.Database.ExecuteSqlRawAsync("UPDATE speed_reading.visualization_scenes SET description='Admin edited scene' WHERE id={0}",
                        oldScenes[0].GetProperty("id").GetGuid());
                else if (change == "hint")
                    await db.Database.ExecuteSqlRawAsync("UPDATE speed_reading.visualization_questions SET hint_text='Admin hint' WHERE id={0}",
                        oldScenes[0].GetProperty("questions")[0].GetProperty("id").GetGuid());
                else if (change == "extra-question")
                {
                    db.VisualizationQuestions.Add(VisualizationQuestion.Create(Guid.NewGuid(),
                        oldScenes[0].GetProperty("id").GetGuid(), "Admin added question", "[\"Bir\",\"İki\"]",
                        "A", "detail", 3, null, student, DateTime.UtcNow));
                    await db.SaveChangesAsync();
                }
                else
                {
                    var payload = JsonNode.Parse(ReadPack("scenes.json"))!;
                    if (change == "duplicate-question") payload[1]!["questions"]![0]!["id"] = payload[0]!["questions"]![0]!["id"]!.GetValue<string>();
                    else if (change == "duplicate-baseline")
                    {
                        var duplicatedBaseline = JsonNode.Parse(baseline)!;
                        duplicatedBaseline[1] = duplicatedBaseline[0]!.DeepClone();
                        baseline = duplicatedBaseline.ToJsonString();
                    }
                    else payload[0]!["questions"]![0]!["options"] = null;
                    sql = ReadPack("apply.template.sql").Replace("{{SCENES}}", payload.ToJsonString()).Replace("{{BASELINE}}", baseline);
                }
                await Assert.ThrowsAsync<Npgsql.PostgresException>(() => RunPackAsync(sql));
                Assert.Equal(60, await db.VisualizationScenes.CountAsync());
                Assert.False(await db.VisualizationScenes.AnyAsync(scene => scene.IsDeleted));
                return;
            }
            await RunPackAsync();
            await RunPackAsync();
            Assert.Equal(120, await db.VisualizationScenes.CountAsync(scene => !scene.IsDeleted));
            Assert.Equal(60, await db.VisualizationScenes.CountAsync(scene => scene.IsDeleted));
            Assert.Equal(540, await db.VisualizationQuestions.CountAsync());
            Assert.Equal(snapshot, (await db.ExerciseSessions.SingleAsync()).SessionDataJson);
            var resumed = await service.StartAsync(student, new() { ExerciseId = exerciseId });
            Assert.Equal(previous.SessionId, resumed.SessionId);
            using var state = JsonDocument.Parse(snapshot);
            foreach (var q in state.RootElement.GetProperty("questions").EnumerateArray())
                Assert.True((await service.ValidateActionAsync(student, previous.SessionId, new() {
                    Action = "answer_question", QuestionId = q.GetProperty("questionId").GetGuid(),
                    Answer = q.GetProperty("correctAnswer").GetString() })).IsCorrect);
            Assert.Equal(100m, (await service.CompleteAsync(student, previous.SessionId, new())).ComprehensionScore);
            var first = await service.StartAsync(student, new() { ExerciseId = exerciseId });
            var firstIds = first.InitialData.GetProperty("visualizationScenes").EnumerateArray()
                .Select(scene => scene.GetProperty("sceneId").GetString()).ToArray();
            Assert.Equal(3, firstIds.Length);
            Assert.Equal(9, first.InitialData.GetProperty("questions").GetArrayLength());
            Assert.DoesNotContain("correctAnswer", first.InitialData.GetRawText(), StringComparison.OrdinalIgnoreCase);
            using var firstState = JsonDocument.Parse((await db.ExerciseSessions.SingleAsync(session => session.Id == first.SessionId)).SessionDataJson);
            foreach (var q in firstState.RootElement.GetProperty("questions").EnumerateArray())
                Assert.True((await service.ValidateActionAsync(student, first.SessionId, new() {
                    Action = "answer_question", QuestionId = q.GetProperty("questionId").GetGuid(),
                    Answer = q.GetProperty("correctAnswer").GetString() })).IsCorrect);
            await service.CompleteAsync(student, first.SessionId, new());
            var second = await service.StartAsync(student, new() { ExerciseId = exerciseId });
            var secondIds = second.InitialData.GetProperty("visualizationScenes").EnumerateArray()
                .Select(scene => scene.GetProperty("sceneId").GetString()).ToArray();
            Assert.Equal(3, secondIds.Length); Assert.Empty(firstIds.Intersect(secondIds));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static string ReadPack(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        return File.ReadAllText(Path.Combine(directory!.FullName, "content-packs", "visualization", "v2", name));
    }

    private async Task RunPackAsync(string? sql = null)
    {
        await using var connection = new Npgsql.NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql ?? ReadPack("apply.sql"), connection);
        await command.ExecuteNonQueryAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selects_matching_scenes_and_persists_only_verified_answers(bool questions)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        // Disposable Testcontainers database, not any production database.
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync(); var student = Guid.NewGuid();
            var type = ExerciseType.Create(Guid.NewGuid(), "Visualization", "Görselleştirme", "visualization");
            var exercise = Exercise.Create("Görselleştirme", "strategy", "{}", 3, student, type.Id);
            var scene = VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Kırmızı bir ev", null, 5, 0, 3, null, student, DateTime.UtcNow);
            var wrong = VisualizationScene.Create(Guid.NewGuid(), exercise.Id, "Başka seviye", null, 5, 1, 1, null, student, DateTime.UtcNow);
            var question = VisualizationQuestion.Create(Guid.NewGuid(), scene.Id, "Ev ne renk?", """["Kırmızı","Mavi"]""", "A", "color", 0, null, student, DateTime.UtcNow);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.VisualizationScenes.AddRange(scene, wrong);
            if (questions) db.VisualizationQuestions.Add(question);
            await db.SaveChangesAsync();
            var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
                .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
            var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
            Assert.Single(started.InitialData.GetProperty("visualizationScenes").EnumerateArray());
            Assert.DoesNotContain("correctAnswer", started.InitialData.GetRawText(), StringComparison.OrdinalIgnoreCase);
            await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.NotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), started.SessionId, new()));
            if (questions)
            {
                var request = new ExerciseActionRequest { Action = "answer_question", QuestionId = question.Id, Answer = "A" };
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, request)).IsCorrect);
                Assert.True((await service.ValidateActionAsync(student, started.SessionId, request)).IsValid);
            }
            var result = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Null(result.RawWPM); Assert.Null(result.WordsRead);
            if (questions) Assert.Equal(100m, result.ComprehensionScore);
            else Assert.Null(result.ComprehensionScore);
            db.ChangeTracker.Clear();
            var repeated = await service.CompleteAsync(student, started.SessionId, new());
            Assert.Equal(result.ComprehensionScore, repeated.ComprehensionScore);
            Assert.Equal(1, await db.ExerciseSessionResults.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
