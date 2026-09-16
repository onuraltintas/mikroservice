namespace SpeedReading.Application.QuestionBank;

public interface ISpeedReadingQuestionBank
{
    Task<QuestionBankPage> GetQuestionsAsync(
        int pageNumber,
        int pageSize,
        int? examType,
        int? difficulty,
        int? category,
        string? searchTerm,
        Guid? ageGroupId,
        CancellationToken cancellationToken);

    Task<ExamQuestionSummary?> GetQuestionAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid> CreateQuestionAsync(
        ExamQuestionRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<bool> UpdateQuestionAsync(
        Guid id,
        ExamQuestionRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<bool> DeleteQuestionAsync(
        Guid id,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<QuestionQualityReview> PreviewQuestionQualityAsync(
        ExamQuestionRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(ExamQuestionQualityAnalyzer.Analyze(request));

    Task<QuestionQualitySummary> GetQuestionQualitySummaryAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(QuestionQualitySummary.Empty);
}

public sealed record QuestionQualityWarning(string Code, string Message);

public sealed record QuestionQualityReview(IReadOnlyList<QuestionQualityWarning> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}

public sealed record QuestionQualitySummary(
    int TotalQuestions,
    IReadOnlyDictionary<string, int> CorrectOptionCounts,
    IReadOnlyList<QuestionQualityWarning> Warnings)
{
    public static readonly QuestionQualitySummary Empty = new(
        0,
        new Dictionary<string, int>(StringComparer.Ordinal),
        []);
}

public static class ExamQuestionQualityAnalyzer
{
    public static QuestionQualityReview Analyze(ExamQuestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var options = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A"] = request.OptionA?.Trim() ?? string.Empty,
            ["B"] = request.OptionB?.Trim() ?? string.Empty,
            ["C"] = request.OptionC?.Trim() ?? string.Empty,
            ["D"] = request.OptionD?.Trim() ?? string.Empty
        };
        if (!string.IsNullOrWhiteSpace(request.OptionE))
            options.Add("E", request.OptionE.Trim());

        var warnings = new List<QuestionQualityWarning>();
        var duplicate = options
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);
        if (duplicate)
            warnings.Add(new("duplicate-options", "Seçeneklerden en az ikisi aynı görünüyor."));

        var correctOption = request.CorrectOption?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!options.TryGetValue(correctOption, out var correctAnswer)
            || string.IsNullOrWhiteSpace(correctAnswer))
        {
            warnings.Add(new("missing-correct-option", "Doğru cevap, dolu bir seçeneği göstermelidir."));
            return new QuestionQualityReview(warnings);
        }

        var otherLengths = options
            .Where(item => item.Key != correctOption && !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => VisibleLength(item.Value))
            .Where(length => length > 0)
            .ToList();
        var correctLength = VisibleLength(correctAnswer);
        // Short labels such as numbers or Roman numerals naturally vary by a character or two;
        // flag a distinctly longer/shorter correct option before it becomes a reliable shortcut.
        const int materialLengthDifference = 6;
        const decimal minimumRelativeLengthRatio = 1.2m;
        if (otherLengths.Count >= 3)
        {
            var longestOtherLength = otherLengths.Max();
            var shortestOtherLength = otherLengths.Min();
            var correctAnswerIsMateriallyLonger = correctLength - longestOtherLength >= materialLengthDifference
                && correctLength >= longestOtherLength * minimumRelativeLengthRatio;
            var correctAnswerIsMateriallyShorter = shortestOtherLength - correctLength >= materialLengthDifference
                && correctLength <= shortestOtherLength / minimumRelativeLengthRatio;
            if (correctAnswerIsMateriallyLonger || correctAnswerIsMateriallyShorter)
            {
                warnings.Add(new(
                    "correct-option-length-cue",
                    "Doğru seçenek diğerlerinden belirgin biçimde uzun veya kısa; uzunluk ipucu oluşturabilir."));
            }
        }

        if (string.IsNullOrWhiteSpace(request.Content) || WordCount(request.Content) < 8)
            warnings.Add(new("short-stem", "Metin çok kısa; soru bağlamı ve zorluk seviyesi tekrar gözden geçirilmeli."));

        return new QuestionQualityReview(warnings);
    }

    public static QuestionQualitySummary SummarizeCorrectOptions(IEnumerable<string> correctOptions)
    {
        ArgumentNullException.ThrowIfNull(correctOptions);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["A"] = 0, ["B"] = 0, ["C"] = 0, ["D"] = 0, ["E"] = 0
        };
        foreach (var option in correctOptions)
        {
            var normalized = option?.Trim().ToUpperInvariant();
            if (normalized is not null && counts.ContainsKey(normalized))
                counts[normalized]++;
        }

        var total = counts.Values.Sum();
        var warnings = new List<QuestionQualityWarning>();
        if (total >= 20 && counts.Values.Max() > total * .35m)
        {
            warnings.Add(new(
                "answer-position-imbalance",
                "Doğru cevapların seçenek dağılımı dengesiz görünüyor; yeni sorularda az kullanılan konumları tercih edin."));
        }
        return new QuestionQualitySummary(total, counts, warnings);
    }

    private static int VisibleLength(string value) =>
        value.Count(character => !char.IsWhiteSpace(character));

    private static int WordCount(string value) =>
        value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}

public sealed record ExamQuestionSummary(
    Guid Id,
    string Content,
    string Question,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    string? OptionE,
    string CorrectOption,
    int ExamType,
    int Difficulty,
    int WordCount,
    string? Topic,
    int Category,
    Guid? TargetAgeGroupId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record QuestionBankPage(
    IReadOnlyList<ExamQuestionSummary> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

public sealed record ExamQuestionRequest(
    string Content,
    string Question,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    string? OptionE,
    string CorrectOption,
    int ExamType,
    int Difficulty,
    int WordCount,
    string? Topic,
    int Category,
    Guid? TargetAgeGroupId = null);
