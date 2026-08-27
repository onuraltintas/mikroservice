using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingProgramBackfillResult(
    int ProgramTemplatesInserted,
    int StudentProgressInserted,
    int DailyExerciseLogsInserted,
    int ExistingRows,
    DateTime CompletedAtUtc);

/// <summary>
/// Copies program templates, student program progress and daily exercise logs
/// after the catalog has been backfilled. User identifiers are references to
/// Identity-owned users and are not resolved through legacy SQL at runtime.
/// </summary>
public sealed class OwnedSpeedReadingProgramBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedSpeedReadingProgramBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await owned.Database.MigrateAsync(cancellationToken);

        var templates = await legacy.ExerciseProgramTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var progress = await legacy.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var logs = await legacy.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var templateIds = templates.Select(item => item.Id).ToHashSet();
        var progressIds = progress.Select(item => item.Id).ToHashSet();
        var exerciseIds = await owned.Exercises
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var exerciseTypeIds = await owned.ExerciseTypes
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        ValidateReferences(
            templates,
            progress,
            logs,
            templateIds,
            progressIds,
            exerciseIds,
            exerciseTypeIds);

        await using var transaction = await owned.Database.BeginTransactionAsync(cancellationToken);
        var existingRows = 0;
        var templatesInserted = 0;
        var progressInserted = 0;
        var logsInserted = 0;

        var existingTemplateIds = await owned.ProgramTemplates
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in templates)
        {
            if (existingTemplateIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.ProgramTemplates.Add(ProgramTemplate.Import(
                source.Id,
                source.Name,
                source.Description,
                source.TargetAgeGroupConfigurationId,
                source.MinAssessmentScore,
                source.MaxAssessmentScore,
                source.WeeklyPatternJson,
                source.InitialDifficultyLevel,
                source.WeeksPerDifficultyIncrease,
                source.MaxDifficultyLevel,
                source.TotalWeeks,
                source.TotalDays,
                source.IsActive,
                source.DisplayOrder,
                source.ProgramType,
                source.ExamType,
                source.IsAssessment,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            templatesInserted++;
        }

        var existingProgressIds = await owned.StudentProgramProgresses
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in progress)
        {
            if (existingProgressIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.StudentProgramProgresses.Add(StudentProgramProgress.Import(
                source.Id,
                source.UserId,
                source.ProgramTemplateId,
                NormalizeUtc(source.AssignedDate),
                Math.Max(source.CurrentDay, 0),
                Math.Max(source.CurrentWeek, 0),
                Math.Max(source.CurrentDifficultyLevel, 0),
                Math.Max(source.DaysCompleted, 0),
                Math.Max(source.ExercisesCompleted, 0),
                NormalizeUtc(source.LastCompletionDate),
                source.IsActive,
                NormalizeUtc(source.CompletedDate),
                Math.Clamp(source.AverageSuccessRate, 0, 100),
                Math.Max(source.CurrentStreak, 0),
                Math.Max(source.LongestStreak, 0),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            progressInserted++;
        }

        var existingLogIds = await owned.DailyExerciseLogs
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in logs)
        {
            if (existingLogIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.DailyExerciseLogs.Add(DailyExerciseLog.Import(
                source.Id,
                source.UserId,
                source.StudentProgramProgressId,
                source.ExerciseId,
                source.ExerciseTypeId,
                source.DayNumber,
                source.WeekNumber,
                source.DifficultyLevel,
                NormalizeUtc(source.CompletedDate),
                Math.Max(source.TimeSpentSeconds, 0),
                Math.Clamp(source.SuccessRate, 0, 100),
                source.IsPassed,
                source.ResultDataJson,
                Math.Max(source.AttemptNumber, 0),
                source.IsRetry,
                source.DevicePlatform,
                Math.Max(source.CorrectCount, 0),
                Math.Max(source.IncorrectCount, 0),
                Math.Max(source.TotalAttempts, 0),
                source.AverageWPM,
                source.AverageComprehension.HasValue
                    ? Math.Clamp(source.AverageComprehension.Value, 0, 100)
                    : null,
                source.DayOfWeek,
                source.TimeOfDay,
                Math.Max(source.AverageResponseTimeMs, 0),
                Math.Max(source.MedianResponseTimeMs, 0),
                Math.Max(source.StdDevResponseTimeMs, 0),
                Math.Max(source.PauseCount, 0),
                Math.Max(source.TotalPausedSeconds, 0),
                source.PerformanceTrend,
                source.IsPersonalBest,
                source.PreviousAverageScore,
                Math.Max(source.CurrentStreak, 0),
                source.EngagementScore,
                source.FrustrationScore,
                source.LearningRate,
                source.ConsistencyScore,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            logsInserted++;
        }

        await owned.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OwnedSpeedReadingProgramBackfillResult(
            templatesInserted,
            progressInserted,
            logsInserted,
            existingRows,
            DateTime.UtcNow);
    }

    private static void ValidateReferences(
        IReadOnlyList<LegacyExerciseProgramTemplate> templates,
        IReadOnlyList<LegacyStudentProgramProgress> progress,
        IReadOnlyList<LegacyDailyExerciseLog> logs,
        IReadOnlySet<Guid> templateIds,
        IReadOnlySet<Guid> progressIds,
        IReadOnlySet<Guid> exerciseIds,
        IReadOnlySet<Guid> exerciseTypeIds)
    {
        foreach (var template in templates)
        {
            if (template.Id == Guid.Empty || string.IsNullOrWhiteSpace(template.Name))
                throw new InvalidOperationException($"Program template {template.Id} is missing required data.");
            if (template.MinAssessmentScore < 0 || template.MaxAssessmentScore < 0)
                throw new InvalidOperationException($"Program template {template.Id} has invalid score bounds.");
        }

        foreach (var item in progress)
        {
            if (!templateIds.Contains(item.ProgramTemplateId))
                throw new InvalidOperationException(
                    $"Student program progress {item.Id} references missing template {item.ProgramTemplateId}.");
            if (item.UserId == Guid.Empty)
                throw new InvalidOperationException($"Student program progress {item.Id} has no user.");
        }

        foreach (var log in logs)
        {
            if (!progressIds.Contains(log.StudentProgramProgressId))
                throw new InvalidOperationException(
                    $"Daily exercise log {log.Id} references missing progress {log.StudentProgramProgressId}.");
            if (!exerciseIds.Contains(log.ExerciseId))
                throw new InvalidOperationException(
                    $"Daily exercise log {log.Id} references missing exercise {log.ExerciseId}.");
            if (!exerciseTypeIds.Contains(log.ExerciseTypeId))
                throw new InvalidOperationException(
                    $"Daily exercise log {log.Id} references missing exercise type {log.ExerciseTypeId}.");
        }
    }

    private static string? ToAuditValue(Guid? value) => value?.ToString();

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue ? NormalizeUtc(value.Value) : null;
}
