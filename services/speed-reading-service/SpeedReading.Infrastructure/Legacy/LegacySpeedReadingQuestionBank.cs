using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.QuestionBank;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingQuestionBank(SpeedReadingDbContext db) : ISpeedReadingQuestionBank
{
    public async Task<QuestionBankPage> GetQuestionsAsync(
        int pageNumber,
        int pageSize,
        int? examType,
        int? difficulty,
        int? category,
        string? searchTerm,
        Guid? ageGroupId,
        CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ExamQuestions
            .AsNoTracking()
            .Where(item => !item.IsDeleted);
        if (examType.HasValue)
        {
            query = query.Where(item => item.ExamType == examType.Value);
        }
        if (difficulty.HasValue)
        {
            query = query.Where(item => item.Difficulty == difficulty.Value);
        }
        if (category.HasValue)
        {
            query = query.Where(item => item.Category == category.Value);
        }
        if (ageGroupId.HasValue)
        {
            query = query.Where(item => item.TargetAgeGroupConfigurationId == ageGroupId.Value);
        }
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim();
            query = query.Where(item => item.Question.Contains(searchTerm) || item.Content.Contains(searchTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(RowSelector)
            .ToListAsync(cancellationToken);

        return new QuestionBankPage(
            rows.Select(ToSummary).ToList(),
            totalCount,
            pageNumber,
            pageSize,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ExamQuestionSummary?> GetQuestionAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.ExamQuestions
            .AsNoTracking()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Select(RowSelector)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToSummary(row);
    }

    public async Task<Guid> CreateQuestionAsync(
        ExamQuestionRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);
        var question = new LegacyExamQuestion
        {
            Id = Guid.NewGuid(),
            Content = request.Content.Trim(),
            Question = request.Question.Trim(),
            OptionA = request.OptionA.Trim(),
            OptionB = request.OptionB.Trim(),
            OptionC = request.OptionC.Trim(),
            OptionD = request.OptionD.Trim(),
            OptionE = NormalizeOptional(request.OptionE),
            CorrectOption = request.CorrectOption.Trim().ToUpperInvariant(),
            ExamType = request.ExamType,
            Difficulty = request.Difficulty,
            WordCount = GetWordCount(request),
            Topic = NormalizeOptional(request.Topic),
            Category = request.Category,
            TargetAgeGroupConfigurationId = request.TargetAgeGroupId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId
        };

        db.ExamQuestions.Add(question);
        await db.SaveChangesAsync(cancellationToken);
        return question.Id;
    }

    public async Task<bool> UpdateQuestionAsync(
        Guid id,
        ExamQuestionRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);
        var question = await db.ExamQuestions
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (question is null)
        {
            return false;
        }

        question.Content = request.Content.Trim();
        question.Question = request.Question.Trim();
        question.OptionA = request.OptionA.Trim();
        question.OptionB = request.OptionB.Trim();
        question.OptionC = request.OptionC.Trim();
        question.OptionD = request.OptionD.Trim();
        question.OptionE = NormalizeOptional(request.OptionE);
        question.CorrectOption = request.CorrectOption.Trim().ToUpperInvariant();
        question.ExamType = request.ExamType;
        question.Difficulty = request.Difficulty;
        question.WordCount = GetWordCount(request);
        question.Topic = NormalizeOptional(request.Topic);
        question.Category = request.Category;
        question.TargetAgeGroupConfigurationId = request.TargetAgeGroupId;
        question.UpdatedAt = DateTime.UtcNow;
        question.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteQuestionAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var question = await db.ExamQuestions
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (question is null)
        {
            return false;
        }

        question.IsDeleted = true;
        question.DeletedAt = DateTime.UtcNow;
        question.DeletedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HardDeleteQuestionAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await db.ExamQuestions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (question is null)
        {
            return false;
        }

        db.ExamQuestions.Remove(question);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureAgeGroupExistsAsync(Guid? ageGroupId, CancellationToken cancellationToken)
    {
        if (ageGroupId.HasValue && !await db.AgeGroupConfigurations.AnyAsync(item => item.Id == ageGroupId.Value, cancellationToken))
        {
            throw new KeyNotFoundException("Target age group not found.");
        }
    }

    private static void ValidateRequest(ExamQuestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content) || string.IsNullOrWhiteSpace(request.Question) ||
            string.IsNullOrWhiteSpace(request.OptionA) || string.IsNullOrWhiteSpace(request.OptionB) ||
            string.IsNullOrWhiteSpace(request.OptionC) || string.IsNullOrWhiteSpace(request.OptionD))
        {
            throw new ArgumentException("Content, Question and options A-D are required.");
        }

        if (request.ExamType is < 0 or > 6)
        {
            throw new ArgumentException("ExamType must be between 0 and 6.");
        }
        if (request.Difficulty is < 1 or > 5)
        {
            throw new ArgumentException("Difficulty must be between 1 and 5.");
        }
        if (request.Category is < 0 or > 17)
        {
            throw new ArgumentException("Category must be between 0 and 17.");
        }
        if (request.WordCount < 0)
        {
            throw new ArgumentException("WordCount cannot be negative.");
        }

        var correctOption = request.CorrectOption.Trim().ToUpperInvariant();
        var availableOptions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = request.OptionA,
            ["B"] = request.OptionB,
            ["C"] = request.OptionC,
            ["D"] = request.OptionD,
            ["E"] = request.OptionE
        };
        if (!availableOptions.TryGetValue(correctOption, out var answer) || string.IsNullOrWhiteSpace(answer))
        {
            throw new ArgumentException("CorrectOption must reference a non-empty option.");
        }
    }

    private static int GetWordCount(ExamQuestionRequest request) =>
        request.WordCount > 0
            ? request.WordCount
            : request.Content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ExamQuestionSummary ToSummary(ExamQuestionRow row) =>
        new(
            row.Id,
            row.Content,
            row.Question,
            row.OptionA,
            row.OptionB,
            row.OptionC,
            row.OptionD,
            row.OptionE,
            row.CorrectOption,
            row.ExamType,
            row.Difficulty,
            row.WordCount,
            row.Topic,
            row.Category,
            row.TargetAgeGroupId,
            row.CreatedAt,
            row.UpdatedAt);

    private static readonly Expression<Func<LegacyExamQuestion, ExamQuestionRow>> RowSelector =
        item => new ExamQuestionRow
        {
            Id = item.Id,
            Content = item.Content,
            Question = item.Question,
            OptionA = item.OptionA,
            OptionB = item.OptionB,
            OptionC = item.OptionC,
            OptionD = item.OptionD,
            OptionE = item.OptionE,
            CorrectOption = item.CorrectOption,
            ExamType = item.ExamType,
            Difficulty = item.Difficulty,
            WordCount = item.WordCount,
            Topic = item.Topic,
            Category = item.Category,
            TargetAgeGroupId = item.TargetAgeGroupConfigurationId,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };

    private sealed class ExamQuestionRow
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = string.Empty;
        public string Question { get; init; } = string.Empty;
        public string OptionA { get; init; } = string.Empty;
        public string OptionB { get; init; } = string.Empty;
        public string OptionC { get; init; } = string.Empty;
        public string OptionD { get; init; } = string.Empty;
        public string? OptionE { get; init; }
        public string CorrectOption { get; init; } = string.Empty;
        public int ExamType { get; init; }
        public int Difficulty { get; init; }
        public int WordCount { get; init; }
        public string? Topic { get; init; }
        public int Category { get; init; }
        public Guid? TargetAgeGroupId { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
