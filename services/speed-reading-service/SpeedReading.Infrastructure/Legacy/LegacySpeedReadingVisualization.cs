using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Visualization;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingVisualization(SpeedReadingDbContext db) : ISpeedReadingVisualization
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SupportedQuestionTypes = ["detail", "color", "position", "count"];

    public async Task<IReadOnlyList<VisualizationSceneSummary>> GetExerciseScenesAsync(
        Guid exerciseId,
        int? limit,
        CancellationToken cancellationToken)
    {
        var exerciseExists = await db.Exercises
            .AsNoTracking()
            .AnyAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken);

        if (!exerciseExists)
        {
            throw new KeyNotFoundException("Exercise not found");
        }

        var scenes = await db.VisualizationScenes
            .AsNoTracking()
            .Where(item => item.ExerciseId == exerciseId && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (limit is > 0 && limit.Value < scenes.Count)
        {
            scenes = scenes
                .OrderBy(_ => Random.Shared.Next())
                .Take(limit.Value)
                .OrderBy(item => item.DisplayOrder)
                .ToList();
        }

        return await BuildSummariesAsync(scenes, cancellationToken);
    }

    public async Task<VisualizationSceneSummary?> GetSceneAsync(
        Guid sceneId,
        CancellationToken cancellationToken)
    {
        var scene = await db.VisualizationScenes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sceneId && !item.IsDeleted, cancellationToken);

        if (scene is null)
        {
            return null;
        }

        var summaries = await BuildSummariesAsync([scene], cancellationToken);
        return summaries.Single();
    }

    public async Task<IReadOnlyList<VisualizationSceneSummary>> GetScenesByDifficultyAsync(
        int difficultyLevel,
        CancellationToken cancellationToken)
    {
        if (difficultyLevel is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(difficultyLevel), "Difficulty level must be between 1 and 5.");
        }

        var scenes = await db.VisualizationScenes
            .AsNoTracking()
            .Where(item => item.DifficultyLevel == difficultyLevel && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);

        return await BuildSummariesAsync(scenes, cancellationToken);
    }

    public async Task<VisualizationScenePage> GetAdminScenesAsync(
        int pageNumber,
        int pageSize,
        int? difficultyLevel,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.VisualizationScenes
            .AsNoTracking()
            .Where(item => !item.IsDeleted);

        if (difficultyLevel.HasValue)
        {
            query = query.Where(item => item.DifficultyLevel == difficultyLevel.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim();
            query = query.Where(item => item.Description.Contains(searchTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var scenes = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.DisplayOrder)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new VisualizationScenePage(
            await BuildSummariesAsync(scenes, cancellationToken),
            totalCount,
            pageNumber,
            pageSize);
    }

    public async Task<IReadOnlyList<VisualizationExerciseOption>> GetExercisesAsync(
        CancellationToken cancellationToken)
    {
        return await db.Exercises
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Title)
            .Select(item => new VisualizationExerciseOption(item.Id, item.Title, item.DifficultyLevel))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateSceneAsync(
        VisualizationSceneRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        ValidateSceneRequest(request);
        await EnsureExerciseExistsAsync(request.ExerciseId, cancellationToken);

        var now = DateTime.UtcNow;
        var scene = new LegacyVisualizationScene
        {
            Id = Guid.NewGuid(),
            ExerciseId = request.ExerciseId,
            Description = request.Description.Trim(),
            ImageUrl = NormalizeOptional(request.ImageUrl),
            Duration = request.Duration,
            DisplayOrder = request.DisplayOrder,
            DifficultyLevel = request.DifficultyLevel,
            TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId,
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.VisualizationScenes.Add(scene);
        AddQuestions(scene.Id, request.Questions, actorId, now);
        await db.SaveChangesAsync(cancellationToken);
        return scene.Id;
    }

    public async Task<bool> UpdateSceneAsync(
        Guid sceneId,
        VisualizationSceneRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        ValidateSceneRequest(request);
        await EnsureExerciseExistsAsync(request.ExerciseId, cancellationToken);

        var scene = await db.VisualizationScenes
            .SingleOrDefaultAsync(item => item.Id == sceneId && !item.IsDeleted, cancellationToken);

        if (scene is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        scene.ExerciseId = request.ExerciseId;
        scene.Description = request.Description.Trim();
        scene.ImageUrl = NormalizeOptional(request.ImageUrl);
        scene.Duration = request.Duration;
        scene.DisplayOrder = request.DisplayOrder;
        scene.DifficultyLevel = request.DifficultyLevel;
        scene.TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId;
        scene.UpdatedAt = now;
        scene.UpdatedBy = actorId;

        var existingQuestions = await db.VisualizationQuestions
            .Where(item => item.SceneId == sceneId && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var question in existingQuestions)
        {
            question.IsDeleted = true;
            question.DeletedAt = now;
            question.DeletedBy = actorId;
        }

        AddQuestions(sceneId, request.Questions, actorId, now);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSceneAsync(
        Guid sceneId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var scene = await db.VisualizationScenes
            .SingleOrDefaultAsync(item => item.Id == sceneId && !item.IsDeleted, cancellationToken);

        if (scene is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        scene.IsDeleted = true;
        scene.DeletedAt = now;
        scene.DeletedBy = actorId;

        var questions = await db.VisualizationQuestions
            .Where(item => item.SceneId == sceneId && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var question in questions)
        {
            question.IsDeleted = true;
            question.DeletedAt = now;
            question.DeletedBy = actorId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<VisualizationImportResult> ImportCsvAsync(
        Stream csv,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new VisualizationImportResult(0, 0, "CSV header is missing.", []);
        }

        var headers = ParseCsvLine(headerLine);
        var headerMap = headers
            .Select((header, index) => new { Header = header.Trim(), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<string>();
        var rowNumber = 1;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var values = ParseCsvLine(line);
                var request = ParseImportRequest(headerMap, values, rowNumber);
                await CreateSceneAsync(request, actorId, cancellationToken);
                successCount++;
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException or KeyNotFoundException)
            {
                failedCount++;
                errors.Add($"Row {rowNumber}: {exception.Message}");
            }
        }

        return new VisualizationImportResult(
            successCount,
            failedCount,
            $"{successCount} visualization scene(s) imported.",
            errors);
    }

    private async Task EnsureExerciseExistsAsync(Guid exerciseId, CancellationToken cancellationToken)
    {
        if (!await db.Exercises.AnyAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken))
        {
            throw new KeyNotFoundException("Exercise not found");
        }
    }

    private async Task<IReadOnlyList<VisualizationSceneSummary>> BuildSummariesAsync(
        IReadOnlyList<LegacyVisualizationScene> scenes,
        CancellationToken cancellationToken)
    {
        if (scenes.Count == 0)
        {
            return [];
        }

        var sceneIds = scenes.Select(item => item.Id).ToArray();
        var questions = await db.VisualizationQuestions
            .AsNoTracking()
            .Where(item => sceneIds.Contains(item.SceneId) && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);

        var questionsByScene = questions
            .GroupBy(item => item.SceneId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<VisualizationQuestionSummary>)group
                    .OrderBy(item => item.DisplayOrder)
                    .Select(ToQuestionSummary)
                    .ToList());

        return scenes
            .Select(scene => new VisualizationSceneSummary(
                scene.Id,
                scene.ExerciseId,
                scene.Description,
                scene.ImageUrl,
                scene.Duration,
                scene.DisplayOrder,
                scene.DifficultyLevel,
                questionsByScene.GetValueOrDefault(scene.Id, []),
                scene.CreatedAt))
            .ToList();
    }

    private void AddQuestions(
        Guid sceneId,
        IReadOnlyList<VisualizationQuestionRequest> questions,
        Guid actorId,
        DateTime now)
    {
        foreach (var request in questions)
        {
            db.VisualizationQuestions.Add(new LegacyVisualizationQuestion
            {
                Id = request.Id.GetValueOrDefault(Guid.NewGuid()),
                SceneId = sceneId,
                QuestionText = request.QuestionText.Trim(),
                OptionsJson = JsonSerializer.Serialize(request.Options, JsonOptions),
                CorrectAnswer = request.CorrectAnswer.Trim(),
                QuestionType = request.QuestionType.Trim().ToLowerInvariant(),
                DisplayOrder = request.DisplayOrder,
                HintText = NormalizeOptional(request.HintText),
                CreatedAt = now,
                CreatedBy = actorId
            });
        }
    }

    private static VisualizationQuestionSummary ToQuestionSummary(LegacyVisualizationQuestion question) =>
        new(
            question.Id,
            question.QuestionText,
            ParseOptions(question.OptionsJson),
            question.CorrectAnswer,
            question.QuestionType,
            question.DisplayOrder,
            question.HintText);

    private static void ValidateSceneRequest(VisualizationSceneRequest request)
    {
        if (request.ExerciseId == Guid.Empty)
        {
            throw new ArgumentException("ExerciseId is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.", nameof(request));
        }

        if (request.Duration is < 1 or > 3600)
        {
            throw new ArgumentException("Duration must be between 1 and 3600 seconds.", nameof(request));
        }

        if (request.DisplayOrder < 0)
        {
            throw new ArgumentException("DisplayOrder cannot be negative.", nameof(request));
        }

        if (request.DifficultyLevel is < 1 or > 5)
        {
            throw new ArgumentException("DifficultyLevel must be between 1 and 5.", nameof(request));
        }

        foreach (var question in request.Questions)
        {
            if (string.IsNullOrWhiteSpace(question.QuestionText))
            {
                throw new ArgumentException("QuestionText is required.", nameof(request));
            }

            var options = question.Options
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Select(option => option.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (options.Length < 2 || string.IsNullOrWhiteSpace(question.CorrectAnswer) ||
                !options.Contains(question.CorrectAnswer.Trim(), StringComparer.Ordinal))
            {
                throw new ArgumentException("Each question needs at least two options and a matching correct answer.", nameof(request));
            }

            if (!SupportedQuestionTypes.Contains(question.QuestionType.Trim().ToLowerInvariant(), StringComparer.Ordinal))
            {
                throw new ArgumentException("QuestionType is invalid.", nameof(request));
            }

            if (question.DisplayOrder < 0)
            {
                throw new ArgumentException("Question DisplayOrder cannot be negative.", nameof(request));
            }
        }
    }

    private static VisualizationSceneRequest ParseImportRequest(
        IReadOnlyDictionary<string, int> headerMap,
        IReadOnlyList<string> values,
        int rowNumber)
    {
        var exerciseId = ParseGuid(GetValue(headerMap, values, "ExerciseId"), "ExerciseId", rowNumber);
        var description = GetValue(headerMap, values, "Description");
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new FormatException("Description is required.");
        }

        var duration = ParseInt(GetValue(headerMap, values, "Duration"), 30, "Duration", rowNumber);
        var difficulty = ParseInt(GetValue(headerMap, values, "DifficultyLevel"), 1, "DifficultyLevel", rowNumber);
        var displayOrder = ParseInt(GetValue(headerMap, values, "DisplayOrder"), rowNumber - 1, "DisplayOrder", rowNumber);
        var questions = new List<VisualizationQuestionRequest>();

        for (var index = 1; index <= 5; index++)
        {
            var questionText = GetValue(headerMap, values, $"Q{index}");
            if (string.IsNullOrWhiteSpace(questionText))
            {
                continue;
            }

            var options = GetValue(headerMap, values, $"O{index}")
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            questions.Add(new VisualizationQuestionRequest(
                questionText,
                options,
                GetValue(headerMap, values, $"A{index}"),
                GetValue(headerMap, values, $"T{index}") is { Length: > 0 } type ? type : "detail",
                index,
                GetValue(headerMap, values, $"H{index}")));
        }

        return new VisualizationSceneRequest(
            exerciseId,
            description,
            GetValue(headerMap, values, "ImageUrl"),
            duration,
            displayOrder,
            difficulty,
            questions);
    }

    private static Guid ParseGuid(string value, string field, int rowNumber) =>
        Guid.TryParse(value, out var result)
            ? result
            : throw new FormatException($"{field} must be a valid GUID (row {rowNumber}).");

    private static int ParseInt(string value, int fallback, string field, int rowNumber) =>
        string.IsNullOrWhiteSpace(value)
            ? fallback
            : int.TryParse(value, out var result)
                ? result
                : throw new FormatException($"{field} must be an integer (row {rowNumber}).");

    private static string GetValue(IReadOnlyDictionary<string, int> headerMap, IReadOnlyList<string> values, string field) =>
        headerMap.TryGetValue(field, out var index) && index < values.Count ? values[index].Trim() : string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> ParseOptions(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(character);
            }
        }

        values.Add(builder.ToString());
        return values;
    }
}
