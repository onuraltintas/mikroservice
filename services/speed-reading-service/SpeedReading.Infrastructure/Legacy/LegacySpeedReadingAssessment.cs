using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingAssessment(SpeedReadingDbContext db) : ISpeedReadingAssessment
{
    public async Task<AssessmentExercisesSummary> GetExercisesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var exercises = await (
            from exercise in db.Exercises.AsNoTracking()
            join exerciseType in db.ExerciseTypes.AsNoTracking()
                on exercise.ExerciseTypeId equals exerciseType.Id into exerciseTypes
            from exerciseType in exerciseTypes.DefaultIfEmpty()
            where !exercise.IsDeleted
            orderby exercise.DifficultyLevel, exercise.Title
            select new
            {
                exercise.Id,
                exercise.Title,
                exercise.Description,
                exercise.DifficultyLevel,
                exercise.ConfigurationJson,
                TypeName = exerciseType == null ? string.Empty : exerciseType.Name
            })
            .Take(3)
            .ToListAsync(cancellationToken);

        var completedIds = await db.StudentExerciseResults
            .AsNoTracking()
            .Where(item => item.StudentId == userId && !item.IsDeleted)
            .Select(item => item.ExerciseId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var items = exercises.Select(item => new AssessmentExerciseItem(
            item.Id,
            item.TypeName,
            item.Title,
            item.Description,
            item.DifficultyLevel,
            completedIds.Contains(item.Id),
            item.ConfigurationJson)).ToList();

        var comprehension = items.FirstOrDefault(item => IsType(item.TypeName, "comprehension", "reading"));
        var visual = items.FirstOrDefault(item => IsType(item.TypeName, "visual", "expansion"));
        var focus = items.FirstOrDefault(item => IsType(item.TypeName, "focus", "fixation"));

        return new AssessmentExercisesSummary(
            comprehension?.Id ?? items.ElementAtOrDefault(0)?.Id,
            visual?.Id ?? items.ElementAtOrDefault(1)?.Id,
            focus?.Id ?? items.ElementAtOrDefault(2)?.Id,
            comprehension?.IsCompleted ?? items.ElementAtOrDefault(0)?.IsCompleted ?? false,
            visual?.IsCompleted ?? items.ElementAtOrDefault(1)?.IsCompleted ?? false,
            focus?.IsCompleted ?? items.ElementAtOrDefault(2)?.IsCompleted ?? false,
            items.Count == 0 ? "Değerlendirme egzersizi bulunamadı." : "Değerlendirme egzersizleri hazır.",
            items);
    }

    public async Task<AssessmentStatusSummary> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var hasCompleted = await db.StudentExerciseResults
            .AsNoTracking()
            .AnyAsync(item => item.StudentId == userId && !item.IsDeleted, cancellationToken);
        return new AssessmentStatusSummary(hasCompleted, null);
    }

    public async Task<AssessmentResultSummary?> CalculateAsync(
        Guid userId,
        AssessmentCalculationRequest? request,
        CancellationToken cancellationToken)
    {
        var results = await db.StudentExerciseResults
            .AsNoTracking()
            .Where(item => item.StudentId == userId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Take(3)
            .ToListAsync(cancellationToken);
        if (results.Count == 0) return null;

        var averageWpm = results.Average(item => item.RawWPM);
        var averageComprehension = results.Average(item => item.ComprehensionScore);
        var level = averageWpm switch
        {
            < 100 => 1,
            < 150 => 2,
            < 200 => 3,
            < 250 => 4,
            < 300 => 5,
            < 400 => 6,
            < 500 => 7,
            _ => 8
        };
        var recommendedLevel = LevelName(level);
        var comprehensionScore = averageComprehension;
        var tachistoscopeScore = averageComprehension;
        var visualExpansionScore = averageComprehension;
        var fixationScore = averageComprehension;
        var focusScore = averageComprehension;

        if (request?.ExerciseResults is { Count: > 0 })
        {
            var exerciseIds = request.ExerciseResults.Select(item => item.ExerciseId).Distinct().ToList();
            var typeNames = await (
                from exercise in db.Exercises.AsNoTracking()
                join exerciseType in db.ExerciseTypes.AsNoTracking()
                    on exercise.ExerciseTypeId equals exerciseType.Id into exerciseTypes
                from exerciseType in exerciseTypes.DefaultIfEmpty()
                where exerciseIds.Contains(exercise.Id)
                select new { exercise.Id, TypeName = exerciseType == null ? string.Empty : exerciseType.Name })
                .ToDictionaryAsync(item => item.Id, item => item.TypeName, cancellationToken);

            foreach (var item in request.ExerciseResults)
            {
                if (!typeNames.TryGetValue(item.ExerciseId, out var typeName)) continue;
                var score = Math.Clamp(item.Score, 0, 100);
                if (IsType(typeName, "comprehension", "reading")) comprehensionScore = score;
                else if (IsType(typeName, "visual", "expansion")) visualExpansionScore = score;
                else if (IsType(typeName, "focus", "fixation")) fixationScore = focusScore = score;
                else if (IsType(typeName, "rsvp", "tachistoscope")) tachistoscopeScore = score;
            }
        }

        var user = await db.Users
            .SingleOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
        if (user is not null)
        {
            user.CurrentLevel = level;
            user.TargetWPM = (int)(averageWpm * 1.2m);
            user.TargetComprehension = Math.Max(70m, averageComprehension);
        }

        var template = await db.ExerciseProgramTemplates
            .AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted
                && item.MinAssessmentScore <= (int)averageComprehension
                && item.MaxAssessmentScore >= (int)averageComprehension)
            .OrderBy(item => item.MinAssessmentScore)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await db.ExerciseProgramTemplates
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.MinAssessmentScore)
                .FirstOrDefaultAsync(cancellationToken);

        if (user is not null) await db.SaveChangesAsync(cancellationToken);

        return new AssessmentResultSummary(
            level,
            recommendedLevel,
            Math.Round(averageWpm, 1),
            (int)averageComprehension,
            Math.Round(averageComprehension, 1),
            (int)comprehensionScore,
            (int)tachistoscopeScore,
            (int)visualExpansionScore,
            (int)fixationScore,
            (int)focusScore,
            user?.TargetWPM,
            user?.TargetComprehension,
            template?.Id,
            null,
            template?.Name ?? "Başlangıç Programı",
            $"{recommendedLevel} okuyucu olarak tespit edildiniz. Ortalama {(int)averageWpm} kelime/dk hızınız ve %{(int)averageComprehension} kavrama oranınız var.");
    }

    public async Task<AssessmentSkipResult?> SkipAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .SingleOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
        if (user is null) return null;

        var ageGroup = user.AgeGroupConfigurationId.HasValue
            ? await db.AgeGroupConfigurations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == user.AgeGroupConfigurationId.Value && !item.IsDeleted, cancellationToken)
            : null;
        user.CurrentLevel = 1;
        user.TargetWPM = ageGroup?.RecommendedWPM ?? 150;
        user.TargetComprehension = ageGroup?.RecommendedComprehension ?? 70;
        await db.SaveChangesAsync(cancellationToken);

        return new AssessmentSkipResult(1, user.TargetWPM, user.TargetComprehension, "Değerlendirme atlandı. Başlangıç seviyesi atandı.");
    }

    public async Task<IReadOnlyList<AssessmentTemplateSummary>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = await db.ExerciseProgramTemplates
            .AsNoTracking()
            .Where(item => item.IsAssessment && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return await MapTemplatesAsync(templates, cancellationToken);
    }

    public async Task<AssessmentTemplateSummary?> GetTemplateByAgeGroupAsync(Guid ageGroupId, CancellationToken cancellationToken)
    {
        var template = await db.ExerciseProgramTemplates
            .AsNoTracking()
            .Where(item => item.TargetAgeGroupConfigurationId == ageGroupId
                && item.IsAssessment && item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (template is null) return null;

        var mapped = await MapTemplatesAsync([template], cancellationToken);
        return mapped.Single();
    }

    public async Task<Guid> CreateTemplateAsync(
        Guid userId,
        CreateAssessmentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTemplate(request.Name, request.TargetAgeGroupId, request.Exercises);
        var ageGroupExists = await db.AgeGroupConfigurations
            .AnyAsync(item => item.Id == request.TargetAgeGroupId && !item.IsDeleted, cancellationToken);
        if (!ageGroupExists) throw new ArgumentException("Age group not found.");

        var row = new LegacyExerciseProgramTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = "Yaş grubu seviye tespit şablonu",
            TargetAgeGroupConfigurationId = request.TargetAgeGroupId,
            MinAssessmentScore = 0,
            MaxAssessmentScore = 100,
            WeeklyPatternJson = BuildWeeklyPattern(request.Exercises),
            InitialDifficultyLevel = 2,
            TotalWeeks = 1,
            TotalDays = 1,
            IsActive = true,
            DisplayOrder = 0,
            IsAssessment = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        db.ExerciseProgramTemplates.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task<bool> UpdateTemplateAsync(
        Guid userId,
        Guid id,
        UpdateAssessmentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTemplate(request.Name, Guid.Empty, request.Exercises);
        var row = await db.ExerciseProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == id && item.IsAssessment && !item.IsDeleted, cancellationToken);
        if (row is null) return false;

        row.Name = request.Name.Trim();
        row.WeeklyPatternJson = BuildWeeklyPattern(request.Exercises);
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTemplateAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.ExerciseProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == id && item.IsAssessment && !item.IsDeleted, cancellationToken);
        if (row is null) return false;
        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<IReadOnlyList<AssessmentTemplateSummary>> MapTemplatesAsync(
        IReadOnlyList<LegacyExerciseProgramTemplate> templates,
        CancellationToken cancellationToken)
    {
        var ageGroupIds = templates.Select(item => item.TargetAgeGroupConfigurationId).Distinct().ToList();
        var ageGroups = await db.AgeGroupConfigurations
            .AsNoTracking()
            .Where(item => ageGroupIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var parsed = templates.SelectMany(item => ParseExerciseEntries(item.WeeklyPatternJson)).ToList();
        var exerciseIds = parsed.Select(item => item.ExerciseId).Distinct().ToList();
        var exercises = await (
            from exercise in db.Exercises.AsNoTracking()
            join exerciseType in db.ExerciseTypes.AsNoTracking()
                on exercise.ExerciseTypeId equals exerciseType.Id into exerciseTypes
            from exerciseType in exerciseTypes.DefaultIfEmpty()
            where exerciseIds.Contains(exercise.Id) && !exercise.IsDeleted
            select new { exercise.Id, exercise.Title, exercise.DifficultyLevel, TypeName = exerciseType == null ? string.Empty : exerciseType.DisplayName })
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return templates.Select(template =>
        {
            ageGroups.TryGetValue(template.TargetAgeGroupConfigurationId, out var ageGroup);
            var exercisesForTemplate = ParseExerciseEntries(template.WeeklyPatternJson)
                .Select((entry, index) =>
                {
                    exercises.TryGetValue(entry.ExerciseId, out var exercise);
                    return new AssessmentTemplateExercise(
                        entry.ExerciseId,
                        exercise?.Title ?? entry.Title ?? string.Empty,
                        exercise?.TypeName ?? entry.Type ?? string.Empty,
                        entry.Difficulty ?? exercise?.DifficultyLevel ?? 1,
                        entry.CustomTitle,
                        entry.CustomDescription,
                        entry.DisplayOrder ?? index);
                }).ToList();

            return new AssessmentTemplateSummary(
                template.Id,
                template.Name,
                template.TargetAgeGroupConfigurationId,
                ageGroup?.Name ?? string.Empty,
                ageGroup?.DisplayName ?? string.Empty,
                exercisesForTemplate,
                template.IsActive,
                template.CreatedAt);
        }).ToList();
    }

    private static IReadOnlyList<TemplateExerciseEntry> ParseExerciseEntries(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!TryGetProperty(document.RootElement, "week1", out var week) || week.ValueKind != JsonValueKind.Array)
                return [];

            var result = new List<TemplateExerciseEntry>();
            foreach (var item in week.EnumerateArray())
            {
                if (!TryGetProperty(item, "exerciseId", out var idProperty)
                    || !Guid.TryParse(idProperty.GetString(), out var exerciseId)) continue;
                result.Add(new TemplateExerciseEntry(
                    exerciseId,
                    GetString(item, "title"),
                    GetString(item, "type"),
                    GetString(item, "customTitle"),
                    GetString(item, "description") ?? GetString(item, "customDescription"),
                    GetInt(item, "difficulty"),
                    GetInt(item, "displayOrder")));
            }
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildWeeklyPattern(IReadOnlyList<AssessmentTemplateExerciseInput> exercises) =>
        JsonSerializer.Serialize(new
        {
            week1 = exercises.OrderBy(item => item.DisplayOrder).Select(item => new
            {
                exerciseId = item.ExerciseId,
                customTitle = item.CustomTitle,
                description = item.CustomDescription ?? string.Empty,
                displayOrder = item.DisplayOrder
            })
        });

    private static void ValidateTemplate(string name, Guid ageGroupId, IReadOnlyList<AssessmentTemplateExerciseInput> exercises)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Name is required and must not exceed 200 characters.");
        if (ageGroupId == Guid.Empty && exercises.Count == 0)
            throw new ArgumentException("At least one assessment exercise is required.");
        if (exercises.Count == 0) throw new ArgumentException("At least one assessment exercise is required.");
        if (exercises.Any(item => item.ExerciseId == Guid.Empty))
            throw new ArgumentException("Every assessment exercise must have an ID.");
    }

    private static bool IsType(string value, params string[] parts) =>
        parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static string LevelName(int level) => level switch
    {
        1 => "Başlangıç",
        2 => "Temel",
        3 => "Orta-Alt",
        4 => "Orta",
        5 => "Orta-Üst",
        6 => "İleri",
        7 => "Uzman",
        8 => "Elit",
        _ => $"Seviye {level}"
    };

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name) =>
        TryGetProperty(element, name, out var property) && property.TryGetInt32(out var value) ? value : null;

    private sealed record TemplateExerciseEntry(
        Guid ExerciseId,
        string? Title,
        string? Type,
        string? CustomTitle,
        string? CustomDescription,
        int? Difficulty,
        int? DisplayOrder);
}
