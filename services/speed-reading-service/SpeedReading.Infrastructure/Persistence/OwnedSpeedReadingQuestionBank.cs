using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.QuestionBank;
using SpeedReading.Domain.QuestionBank;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingQuestionBank(OwnedSpeedReadingDbContext db) : ISpeedReadingQuestionBank
{
    private const string CreateScope = "speed-reading.question-bank.create";
    private const string UpdateScope = "speed-reading.question-bank.update";
    private const string DeleteScope = "speed-reading.question-bank.delete";

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

    public async Task<Guid> CreateQuestionAsync(
        ExamQuestionRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        OwnedContentMutationIdempotency.Validate(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = OwnedContentMutationIdempotency.CreateRequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await OwnedContentMutationIdempotency.GetAsync(db, CreateScope, key, cancellationToken);
        if (existing is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(existing, requestHash);
            return existing.ResourceId;
        }

        ValidateWordCount(request);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);
        var now = DateTime.UtcNow;
        var item = ExamQuestion.Create(Guid.NewGuid(), request.Content, request.Question, request.OptionA,
            request.OptionB, request.OptionC, request.OptionD, request.OptionE, request.CorrectOption,
            request.ExamType, request.Difficulty, request.WordCount, request.Topic, request.Category,
            request.TargetAgeGroupId, now, actorId);
        db.ExamQuestions.Add(item);
        OwnedContentMutationIdempotency.Add(db, CreateScope, key, requestHash, item.Id, now);
        var concurrent = await OwnedContentMutationIdempotency.SaveAsync(db, CreateScope, key, cancellationToken);
        if (concurrent is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(concurrent, requestHash);
            return concurrent.ResourceId;
        }
        return item.Id;
    }

    public async Task<bool> UpdateQuestionAsync(
        Guid id,
        ExamQuestionRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        OwnedContentMutationIdempotency.Validate(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = OwnedContentMutationIdempotency.CreateRequestHash(actorId, UpdateScope, id, request);
        var existing = await OwnedContentMutationIdempotency.GetAsync(db, UpdateScope, key, cancellationToken);
        if (existing is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(existing, requestHash);
            return true;
        }

        ValidateWordCount(request);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupId, cancellationToken);
        var item = await db.ExamQuestions.SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        if (item is null) return false;
        item.Update(request.Content, request.Question, request.OptionA, request.OptionB, request.OptionC,
            request.OptionD, request.OptionE, request.CorrectOption, request.ExamType, request.Difficulty,
            request.WordCount, request.Topic, request.Category, request.TargetAgeGroupId, actorId, DateTime.UtcNow);
        OwnedContentMutationIdempotency.Add(db, UpdateScope, key, requestHash, item.Id, DateTime.UtcNow);
        var concurrent = await OwnedContentMutationIdempotency.SaveAsync(db, UpdateScope, key, cancellationToken);
        if (concurrent is not null)
            OwnedContentMutationIdempotency.EnsureReplayMatches(concurrent, requestHash);
        return true;
    }

    public async Task<bool> DeleteQuestionAsync(
        Guid id,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        OwnedContentMutationIdempotency.Validate(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = OwnedContentMutationIdempotency.CreateRequestHash(actorId, DeleteScope, id);
        var existing = await OwnedContentMutationIdempotency.GetAsync(db, DeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(existing, requestHash);
            return true;
        }

        var item = await db.ExamQuestions.SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken);
        if (item is null) return false;
        var now = DateTime.UtcNow;
        item.Delete(actorId, now);
        OwnedContentMutationIdempotency.Add(db, DeleteScope, key, requestHash, item.Id, now);
        var concurrent = await OwnedContentMutationIdempotency.SaveAsync(db, DeleteScope, key, cancellationToken);
        if (concurrent is not null)
            OwnedContentMutationIdempotency.EnsureReplayMatches(concurrent, requestHash);
        return true;
    }

    public async Task<QuestionQualitySummary> GetQuestionQualitySummaryAsync(
        CancellationToken cancellationToken)
    {
        var correctOptions = await db.ExamQuestions.AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => item.CorrectOption)
            .ToListAsync(cancellationToken);
        return ExamQuestionQualityAnalyzer.SummarizeCorrectOptions(correctOptions);
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
