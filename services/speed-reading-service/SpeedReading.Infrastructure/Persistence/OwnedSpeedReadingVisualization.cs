using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Visualization;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Visualization;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingVisualization(OwnedSpeedReadingDbContext db) : ISpeedReadingVisualization
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SupportedQuestionTypes = ["detail", "color", "position", "count"];

    public async Task<IReadOnlyList<VisualizationSceneSummary>> GetExerciseScenesAsync(Guid exerciseId, int? limit, CancellationToken cancellationToken)
    {
        if (!await db.Exercises.AsNoTracking().AnyAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken))
            throw new KeyNotFoundException("Exercise not found");
        var scenes = await db.VisualizationScenes.AsNoTracking()
            .Where(item => item.ExerciseId == exerciseId && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder).ToListAsync(cancellationToken);
        if (limit is > 0 && limit.Value < scenes.Count)
            scenes = scenes.OrderBy(_ => Random.Shared.Next()).Take(limit.Value).OrderBy(item => item.DisplayOrder).ToList();
        return await BuildSummariesAsync(scenes, cancellationToken);
    }

    public async Task<VisualizationSceneSummary?> GetSceneAsync(Guid sceneId, CancellationToken cancellationToken)
    {
        var scene = await db.VisualizationScenes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sceneId && !item.IsDeleted, cancellationToken);
        return scene is null ? null : (await BuildSummariesAsync([scene], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<VisualizationSceneSummary>> GetScenesByDifficultyAsync(int difficultyLevel, CancellationToken cancellationToken)
    {
        if (difficultyLevel is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(difficultyLevel));
        var scenes = await db.VisualizationScenes.AsNoTracking()
            .Where(item => item.DifficultyLevel == difficultyLevel && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder).ToListAsync(cancellationToken);
        return await BuildSummariesAsync(scenes, cancellationToken);
    }

    public async Task<VisualizationScenePage> GetAdminScenesAsync(int pageNumber, int pageSize, int? difficultyLevel, string? searchTerm, CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.VisualizationScenes.AsNoTracking().Where(item => !item.IsDeleted);
        if (difficultyLevel.HasValue) query = query.Where(item => item.DifficultyLevel == difficultyLevel.Value);
        if (!string.IsNullOrWhiteSpace(searchTerm)) query = query.Where(item => item.Description.Contains(searchTerm.Trim()));
        var totalCount = await query.CountAsync(cancellationToken);
        var scenes = await query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.DisplayOrder)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new VisualizationScenePage(await BuildSummariesAsync(scenes, cancellationToken), totalCount, pageNumber, pageSize);
    }

    public Task<IReadOnlyList<VisualizationExerciseOption>> GetExercisesAsync(CancellationToken cancellationToken) =>
        db.Exercises.AsNoTracking().Where(item => !item.IsDeleted).OrderBy(item => item.Title)
            .Select(item => new VisualizationExerciseOption(item.Id, item.Title, item.DifficultyLevel))
            .ToListAsync(cancellationToken).ContinueWith(task => (IReadOnlyList<VisualizationExerciseOption>)task.Result, cancellationToken);

    public async Task<Guid> CreateSceneAsync(VisualizationSceneRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ValidateSceneRequest(request);
        await EnsureExerciseExistsAsync(request.ExerciseId, cancellationToken);
        var now = DateTime.UtcNow;
        var scene = VisualizationScene.Create(Guid.NewGuid(), request.ExerciseId, request.Description, request.ImageUrl,
            request.Duration, request.DisplayOrder, request.DifficultyLevel, request.TargetAgeGroupConfigurationId, actorId, now);
        db.VisualizationScenes.Add(scene);
        AddQuestions(scene.Id, request.Questions, actorId, now);
        await db.SaveChangesAsync(cancellationToken);
        return scene.Id;
    }

    public async Task<bool> UpdateSceneAsync(Guid sceneId, VisualizationSceneRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ValidateSceneRequest(request);
        await EnsureExerciseExistsAsync(request.ExerciseId, cancellationToken);
        var scene = await db.VisualizationScenes.SingleOrDefaultAsync(item => item.Id == sceneId && !item.IsDeleted, cancellationToken);
        if (scene is null) return false;
        var now = DateTime.UtcNow;
        scene.Update(request.ExerciseId, request.Description, request.ImageUrl, request.Duration, request.DisplayOrder,
            request.DifficultyLevel, request.TargetAgeGroupConfigurationId, actorId, now);
        var questions = await db.VisualizationQuestions.Where(item => item.SceneId == sceneId && !item.IsDeleted).ToListAsync(cancellationToken);
        foreach (var question in questions) question.Delete(actorId, now);
        AddQuestions(sceneId, request.Questions, actorId, now);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSceneAsync(Guid sceneId, Guid actorId, CancellationToken cancellationToken)
    {
        var scene = await db.VisualizationScenes.SingleOrDefaultAsync(item => item.Id == sceneId && !item.IsDeleted, cancellationToken);
        if (scene is null) return false;
        var now = DateTime.UtcNow; scene.Delete(actorId, now);
        var questions = await db.VisualizationQuestions.Where(item => item.SceneId == sceneId && !item.IsDeleted).ToListAsync(cancellationToken);
        foreach (var question in questions) question.Delete(actorId, now);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<VisualizationImportResult> ImportCsvAsync(Stream csv, Guid actorId, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(csv, Encoding.UTF8, true, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine)) return new VisualizationImportResult(0, 0, "CSV header is missing.", []);
        var headers = ParseCsvLine(headerLine).Select((value, index) => new { Header = value.Trim(), Index = index })
            .Where(item => item.Header.Length > 0).ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);
        var success = 0; var failed = 0; var errors = new List<string>(); var row = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            row++; if (string.IsNullOrWhiteSpace(line)) continue;
            try { await CreateSceneAsync(ParseImportRequest(headers, ParseCsvLine(line), row), actorId, cancellationToken); success++; }
            catch (Exception exception) when (exception is FormatException or ArgumentException or KeyNotFoundException)
            { failed++; errors.Add($"Row {row}: {exception.Message}"); }
        }
        return new VisualizationImportResult(success, failed, $"{success} visualization scene(s) imported.", errors);
    }

    private async Task EnsureExerciseExistsAsync(Guid exerciseId, CancellationToken cancellationToken)
    {
        if (!await db.Exercises.AnyAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken))
            throw new KeyNotFoundException("Exercise not found");
    }

    private async Task<IReadOnlyList<VisualizationSceneSummary>> BuildSummariesAsync(IReadOnlyList<VisualizationScene> scenes, CancellationToken cancellationToken)
    {
        if (scenes.Count == 0) return [];
        var ids = scenes.Select(item => item.Id).ToArray();
        var questions = await db.VisualizationQuestions.AsNoTracking()
            .Where(item => ids.Contains(item.SceneId) && !item.IsDeleted).OrderBy(item => item.DisplayOrder).ToListAsync(cancellationToken);
        var byScene = questions.GroupBy(item => item.SceneId).ToDictionary(group => group.Key,
            group => (IReadOnlyList<VisualizationQuestionSummary>)group.Select(ToQuestionSummary).ToList());
        return scenes.Select(scene => new VisualizationSceneSummary(scene.Id, scene.ExerciseId, scene.Description, scene.ImageUrl,
            scene.Duration, scene.DisplayOrder, scene.DifficultyLevel, byScene.GetValueOrDefault(scene.Id, []), scene.CreatedAt)).ToList();
    }

    private void AddQuestions(Guid sceneId, IReadOnlyList<VisualizationQuestionRequest> questions, Guid actorId, DateTime now)
    {
        foreach (var request in questions)
            db.VisualizationQuestions.Add(VisualizationQuestion.Create(request.Id.GetValueOrDefault(Guid.NewGuid()), sceneId,
                request.QuestionText, JsonSerializer.Serialize(request.Options, JsonOptions), request.CorrectAnswer,
                request.QuestionType, request.DisplayOrder, request.HintText, actorId, now));
    }

    private static VisualizationQuestionSummary ToQuestionSummary(VisualizationQuestion question) => new(
        question.Id, question.QuestionText, ParseOptions(question.OptionsJson), question.CorrectAnswer, question.QuestionType,
        question.DisplayOrder, question.HintText);

    private static void ValidateSceneRequest(VisualizationSceneRequest request)
    {
        if (request.ExerciseId == Guid.Empty) throw new ArgumentException("ExerciseId is required.");
        if (string.IsNullOrWhiteSpace(request.Description)) throw new ArgumentException("Description is required.");
        if (request.Duration is < 1 or > 3600) throw new ArgumentException("Duration must be between 1 and 3600 seconds.");
        if (request.DisplayOrder < 0) throw new ArgumentException("DisplayOrder cannot be negative.");
        if (request.DifficultyLevel is < 1 or > 5) throw new ArgumentException("DifficultyLevel must be between 1 and 5.");
        foreach (var question in request.Questions)
        {
            var options = question.Options.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray();
            if (string.IsNullOrWhiteSpace(question.QuestionText) || options.Length < 2 || string.IsNullOrWhiteSpace(question.CorrectAnswer) ||
                !options.Contains(question.CorrectAnswer.Trim(), StringComparer.Ordinal)) throw new ArgumentException("Each question needs at least two options and a matching correct answer.");
            if (!SupportedQuestionTypes.Contains(question.QuestionType.Trim().ToLowerInvariant(), StringComparer.Ordinal)) throw new ArgumentException("QuestionType is invalid.");
            if (question.DisplayOrder < 0) throw new ArgumentException("Question DisplayOrder cannot be negative.");
        }
    }

    private static VisualizationSceneRequest ParseImportRequest(IReadOnlyDictionary<string, int> headers, IReadOnlyList<string> values, int row)
    {
        var exerciseId = Guid.TryParse(GetValue(headers, values, "ExerciseId"), out var id) ? id : throw new FormatException($"ExerciseId must be a valid GUID (row {row}).");
        var description = GetValue(headers, values, "Description"); if (description.Length == 0) throw new FormatException("Description is required.");
        var duration = ParseInt(GetValue(headers, values, "Duration"), 30, "Duration", row);
        var difficulty = ParseInt(GetValue(headers, values, "DifficultyLevel"), 1, "DifficultyLevel", row);
        var displayOrder = ParseInt(GetValue(headers, values, "DisplayOrder"), row - 1, "DisplayOrder", row);
        var questions = new List<VisualizationQuestionRequest>();
        for (var index = 1; index <= 5; index++)
        {
            var text = GetValue(headers, values, $"Q{index}"); if (text.Length == 0) continue;
            questions.Add(new VisualizationQuestionRequest(text, GetValue(headers, values, $"O{index}").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                GetValue(headers, values, $"A{index}"), string.IsNullOrWhiteSpace(GetValue(headers, values, $"T{index}")) ? "detail" : GetValue(headers, values, $"T{index}"), index, GetValue(headers, values, $"H{index}")));
        }
        return new VisualizationSceneRequest(exerciseId, description, GetValue(headers, values, "ImageUrl"), duration, displayOrder, difficulty, questions);
    }

    private static int ParseInt(string value, int fallback, string field, int row) => string.IsNullOrWhiteSpace(value) ? fallback : int.TryParse(value, out var result) ? result : throw new FormatException($"{field} must be an integer (row {row}).");
    private static string GetValue(IReadOnlyDictionary<string, int> headers, IReadOnlyList<string> values, string field) => headers.TryGetValue(field, out var index) && index < values.Count ? values[index].Trim() : string.Empty;
    private static List<string> ParseOptions(string value) { try { return JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? []; } catch (JsonException) { return []; } }
    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>(); var builder = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++) { var c = line[i]; if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { builder.Append('"'); i++; } else quoted = !quoted; } else if (c == ',' && !quoted) { values.Add(builder.ToString()); builder.Clear(); } else builder.Append(c); }
        values.Add(builder.ToString()); return values;
    }
}
