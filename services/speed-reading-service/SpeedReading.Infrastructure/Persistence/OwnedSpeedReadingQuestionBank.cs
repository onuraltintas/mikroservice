using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.QuestionBank;
using SpeedReading.Domain.QuestionBank;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingQuestionBank(OwnedSpeedReadingDbContext db) : ISpeedReadingQuestionBank
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
        var query = db.ExamQuestions.AsNoTracking().Where(item => !item.IsDeleted);
        if (examType.HasValue) query = query.Where(item => item.ExamType == examType.Value);
        if (difficulty.HasValue) query = query.Where(item => item.Difficulty == difficulty.Value);
        if (category.HasValue) query = query.Where(item => item.Category == category.Value);
        if (ageGroupId.HasValue) query = query.Where(item => item.TargetAgeGroupId == ageGroupId.Value);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(item => item.Question.Contains(term) || item.Content.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.CreatedAt)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new QuestionBankPage(rows.Select(ToSummary).ToList(), totalCount, pageNumber, pageSize,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ExamQuestionSummary?> GetQuestionAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.ExamQuestions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        return item is null ? null : ToSummary(item);
    }

    public async Task<Guid> CreateQuestionAsync(ExamQuestionRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ValidateWordCount(request);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);
        var item = ExamQuestion.Create(Guid.NewGuid(), request.Content, request.Question, request.OptionA,
            request.OptionB, request.OptionC, request.OptionD, request.OptionE, request.CorrectOption,
            request.ExamType, request.Difficulty, request.WordCount, request.Topic, request.Category,
            request.TargetAgeGroupId, DateTime.UtcNow, actorId);
        db.ExamQuestions.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    public async Task<bool> UpdateQuestionAsync(Guid id, ExamQuestionRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ValidateWordCount(request);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);
        var item = await db.ExamQuestions.SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        if (item is null) return false;
        item.Update(request.Content, request.Question, request.OptionA, request.OptionB, request.OptionC,
            request.OptionD, request.OptionE, request.CorrectOption, request.ExamType, request.Difficulty,
            request.WordCount, request.Topic, request.Category, request.TargetAgeGroupId, actorId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteQuestionAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var item = await db.ExamQuestions.SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        if (item is null) return false;
        item.Delete(actorId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HardDeleteQuestionAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.ExamQuestions.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) return false;
        db.ExamQuestions.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureAgeGroupExistsAsync(Guid? ageGroupId, CancellationToken cancellationToken)
    {
        if (ageGroupId.HasValue && !await db.AgeGroupConfigurations.AnyAsync(item => item.Id == ageGroupId.Value, cancellationToken))
            throw new KeyNotFoundException("Target age group not found.");
    }

    private static void ValidateWordCount(ExamQuestionRequest request)
    {
        if (request.WordCount < 0) throw new ArgumentException("WordCount cannot be negative.", nameof(request));
    }

    private static ExamQuestionSummary ToSummary(ExamQuestion item) => new(
        item.Id, item.Content, item.Question, item.OptionA, item.OptionB, item.OptionC, item.OptionD,
        item.OptionE, item.CorrectOption, item.ExamType, item.Difficulty, item.WordCount, item.Topic,
        item.Category, item.TargetAgeGroupId, item.CreatedAt, item.UpdatedAt);
}
