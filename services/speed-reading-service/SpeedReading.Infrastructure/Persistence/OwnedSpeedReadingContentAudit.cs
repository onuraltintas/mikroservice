using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.QuestionBank;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Read-only audit of owned, non-reading content. It is intentionally separate
/// from write paths so existing content is reported before any editorial action.
/// </summary>
public sealed class OwnedSpeedReadingContentAudit(OwnedSpeedReadingDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OwnedContentAuditReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var questions = await db.ExamQuestions.AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => new QuestionAuditRow(
                item.Id, item.Content, item.Question, item.OptionA, item.OptionB, item.OptionC,
                item.OptionD, item.OptionE, item.CorrectOption, item.ExamType, item.Difficulty,
                item.WordCount, item.Topic, item.Category, item.TargetAgeGroupId))
            .ToListAsync(cancellationToken);
        var vocabulary = await db.VocabularyItems.AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => new VocabularyAuditRow(item.Id, item.Word, item.Definition, item.ExampleSentence,
                item.Synonyms, item.Antonyms, item.Category, item.DifficultyLevel, item.TargetAgeGroupId))
            .ToListAsync(cancellationToken);
        var scenes = await db.VisualizationScenes.AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => new SceneAuditRow(item.Id, item.ExerciseId, item.Description, item.Mode))
            .ToListAsync(cancellationToken);
        var sceneIds = scenes.Select(item => item.Id).ToArray();
        var visualizationQuestions = await db.VisualizationQuestions.AsNoTracking()
            .Where(item => sceneIds.Contains(item.SceneId) && !item.IsDeleted)
            .Select(item => new VisualizationQuestionAuditRow(item.Id, item.SceneId, item.QuestionText,
                item.OptionsJson, item.CorrectAnswer))
            .ToListAsync(cancellationToken);

        var findings = new List<OwnedContentAuditFinding>();
        AddQuestionFindings(questions, findings);
        AddVocabularyFindings(vocabulary, findings);
        AddVisualizationFindings(scenes, visualizationQuestions, findings);
        return new OwnedContentAuditReport(questions.Count, vocabulary.Count, scenes.Count, findings);
    }

    private static void AddQuestionFindings(
        IReadOnlyList<QuestionAuditRow> questions,
        ICollection<OwnedContentAuditFinding> findings)
    {
        var reviews = questions.Select(item => new
        {
            item.Id,
            Review = ExamQuestionQualityAnalyzer.Analyze(new ExamQuestionRequest(
                item.Content, item.Question, item.OptionA, item.OptionB, item.OptionC, item.OptionD,
                item.OptionE, item.CorrectOption, item.ExamType, item.Difficulty, item.WordCount,
                item.Topic, item.Category, item.TargetAgeGroupId))
        }).ToList();
        foreach (var group in reviews.SelectMany(item => item.Review.Warnings.Select(warning => new { item.Id, warning }))
                     .GroupBy(item => item.warning.Code))
        {
            AddFinding(findings, "question-bank", group.Key, group.First().warning.Message,
                group.Select(item => item.Id));
        }

        var distribution = ExamQuestionQualityAnalyzer.SummarizeCorrectOptions(questions.Select(item => item.CorrectOption));
        foreach (var warning in distribution.Warnings)
            AddFinding(findings, "question-bank", warning.Code, warning.Message, []);
    }

    private static void AddVocabularyFindings(
        IReadOnlyList<VocabularyAuditRow> vocabulary,
        ICollection<OwnedContentAuditFinding> findings)
    {
        foreach (var group in vocabulary.GroupBy(item => new
                 {
                     Word = Normalize(item.Word),
                     Category = Normalize(item.Category),
                     item.TargetAgeGroupId
                 }).Where(group => group.Count() > 1))
        {
            AddFinding(findings, "vocabulary", "duplicate-word",
                "Aynı kelime, kategori ve yaş grubu için birden fazla kayıt var.", group.Select(item => item.Id));
        }
        AddFindingForRows(findings, "vocabulary", "short-definition",
            "Tanım çok kısa; öğrencinin anlayacağı açıklama ile genişletin.",
            vocabulary.Where(item => WordCount(item.Definition) < 3).Select(item => item.Id));
        AddFindingForRows(findings, "vocabulary", "missing-example",
            "Örnek cümlesi olmayan kelime kaydı var.",
            vocabulary.Where(item => string.IsNullOrWhiteSpace(item.ExampleSentence)).Select(item => item.Id));
        AddFindingForRows(findings, "vocabulary", "synonym-antonym-overlap",
            "Eş anlamlı ve zıt anlamlı listelerinde ortak ifade var.",
            vocabulary.Where(item => HasOverlap(item.Synonyms, item.Antonyms)).Select(item => item.Id));
    }

    private static void AddVisualizationFindings(
        IReadOnlyList<SceneAuditRow> scenes,
        IReadOnlyList<VisualizationQuestionAuditRow> questions,
        ICollection<OwnedContentAuditFinding> findings)
    {
        var byScene = questions.GroupBy(item => item.SceneId).ToDictionary(group => group.Key, group => group.ToList());
        AddFindingForRows(findings, "visualization", "assessment-without-question",
            "Ölçme modunda sorusu olmayan sahne var.",
            scenes.Where(scene => scene.Mode.Equals("assessment", StringComparison.OrdinalIgnoreCase)
                                  && !byScene.ContainsKey(scene.Id)).Select(scene => scene.Id));
        AddFindingForRows(findings, "visualization", "duplicate-scene-description",
            "Aynı egzersizde aynı sahne açıklaması birden fazla kez kullanılmış.",
            scenes.GroupBy(scene => new { scene.ExerciseId, Description = Normalize(scene.Description) })
                .Where(group => group.Count() > 1).SelectMany(group => group.Select(item => item.Id)));

        var invalidQuestions = questions.Where(question => !HasValidVisualizationQuestion(question)).Select(question => question.Id);
        AddFindingForRows(findings, "visualization", "invalid-question-options",
            "Görselleştirme sorusunda seçenekler veya doğru cevap geçerli değil.", invalidQuestions);
    }

    private static bool HasValidVisualizationQuestion(VisualizationQuestionAuditRow question)
    {
        try
        {
            var options = JsonSerializer.Deserialize<List<string>>(question.OptionsJson, JsonOptions)
                ?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.Ordinal)
                .ToArray() ?? [];
            return !string.IsNullOrWhiteSpace(question.QuestionText)
                && options.Length >= 2
                && options.Contains(question.CorrectAnswer.Trim(), StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasOverlap(string? first, string? second)
    {
        var firstValues = Tokenize(first).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return firstValues.Overlaps(Tokenize(second));
    }

    private static IEnumerable<string> Tokenize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => item.Length > 0);

    private static int WordCount(string value) =>
        value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static void AddFindingForRows(
        ICollection<OwnedContentAuditFinding> findings,
        string area,
        string code,
        string message,
        IEnumerable<Guid> ids)
    {
        var collected = ids.Distinct().ToArray();
        if (collected.Length > 0) AddFinding(findings, area, code, message, collected);
    }

    private static void AddFinding(
        ICollection<OwnedContentAuditFinding> findings,
        string area,
        string code,
        string message,
        IEnumerable<Guid> ids) =>
        findings.Add(new OwnedContentAuditFinding(area, code, message, ids.Distinct().Count(), ids.Distinct().Take(10).ToArray()));

    private sealed record QuestionAuditRow(Guid Id, string Content, string Question, string OptionA, string OptionB,
        string OptionC, string OptionD, string? OptionE, string CorrectOption, int ExamType, int Difficulty,
        int WordCount, string? Topic, int Category, Guid? TargetAgeGroupId);
    private sealed record VocabularyAuditRow(Guid Id, string Word, string Definition, string? ExampleSentence,
        string? Synonyms, string? Antonyms, string Category, int DifficultyLevel, Guid? TargetAgeGroupId);
    private sealed record SceneAuditRow(Guid Id, Guid ExerciseId, string Description, string Mode);
    private sealed record VisualizationQuestionAuditRow(Guid Id, Guid SceneId, string QuestionText, string OptionsJson,
        string CorrectAnswer);
}

public sealed record OwnedContentAuditFinding(
    string Area,
    string Code,
    string Message,
    int AffectedCount,
    IReadOnlyList<Guid> SampleIds);

public sealed record OwnedContentAuditReport(
    int QuestionBankCount,
    int VocabularyCount,
    int VisualizationSceneCount,
    IReadOnlyList<OwnedContentAuditFinding> Findings);
