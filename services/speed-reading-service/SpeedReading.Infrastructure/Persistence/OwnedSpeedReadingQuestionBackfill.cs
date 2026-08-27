using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.QuestionBank;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingQuestionBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedQuestionBackfillResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var source = await legacy.ExamQuestions.AsNoTracking().ToListAsync(cancellationToken);
        var ageGroupIds = source.Where(item => item.TargetAgeGroupConfigurationId.HasValue)
            .Select(item => item.TargetAgeGroupConfigurationId!.Value).Distinct().ToArray();
        var missingAgeGroups = await owned.AgeGroupConfigurations.AsNoTracking()
            .Where(item => ageGroupIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var existingIds = await owned.ExamQuestions.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var imported = 0;
        var skipped = 0;
        foreach (var item in source)
        {
            if (existingIds.Contains(item.Id))
            {
                skipped++;
                continue;
            }
            if (item.TargetAgeGroupConfigurationId is { } ageGroupId && !missingAgeGroups.Contains(ageGroupId))
                throw new InvalidOperationException($"Exam question {item.Id} references missing age group {ageGroupId}.");

            var question = ExamQuestion.Import(
                item.Id, item.Content, item.Question, item.OptionA, item.OptionB, item.OptionC, item.OptionD,
                item.OptionE, item.CorrectOption, item.ExamType, item.Difficulty, item.WordCount, item.Topic,
                item.Category, item.TargetAgeGroupConfigurationId, item.CreatedAt, item.CreatedBy,
                item.UpdatedAt, item.UpdatedBy == Guid.Empty ? null : item.UpdatedBy,
                item.IsDeleted, item.DeletedAt, item.DeletedBy == Guid.Empty ? null : item.DeletedBy);
            owned.ExamQuestions.Add(question);
            imported++;
        }

        if (imported > 0)
            await owned.SaveChangesAsync(cancellationToken);
        return new OwnedQuestionBackfillResult(source.Count, imported, skipped);
    }
}

public sealed record OwnedQuestionBackfillResult(int SourceCount, int ImportedCount, int SkippedCount);
