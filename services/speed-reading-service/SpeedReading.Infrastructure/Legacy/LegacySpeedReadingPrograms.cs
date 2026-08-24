using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingPrograms(SpeedReadingDbContext db) : ILegacySpeedReadingPrograms
{
    public async Task<IReadOnlyList<ExerciseProgramTemplateSummary>> GetProgramTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        await db.ExerciseProgramTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Select(item => new ExerciseProgramTemplateSummary(
                item.Id,
                item.Name,
                item.Description,
                item.MinAssessmentScore,
                item.MaxAssessmentScore,
                item.InitialDifficultyLevel,
                item.MaxDifficultyLevel,
                item.TotalWeeks,
                item.TotalDays,
                item.IsActive,
                item.DisplayOrder,
                item.ProgramType,
                item.ExamType,
                item.IsAssessment))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExerciseProgramTemplateAdminSummary>> GetProgramTemplateAdminSummariesAsync(
        CancellationToken cancellationToken = default) =>
        await db.ExerciseProgramTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Select(item => new ExerciseProgramTemplateAdminSummary(
                item.Id,
                item.Name,
                item.Description,
                item.TargetAgeGroupConfigurationId,
                item.MinAssessmentScore,
                item.MaxAssessmentScore,
                item.WeeklyPatternJson,
                item.InitialDifficultyLevel,
                item.WeeksPerDifficultyIncrease,
                item.MaxDifficultyLevel,
                item.TotalWeeks,
                item.TotalDays,
                item.IsActive,
                item.DisplayOrder,
                item.ProgramType,
                item.ExamType,
                item.IsAssessment))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentProgramProgressSummary>> GetStudentProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await db.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.AssignedDate)
            .Select(item => new StudentProgramProgressSummary(
                item.Id,
                item.ProgramTemplateId,
                item.AssignedDate,
                item.CurrentDay,
                item.CurrentWeek,
                item.CurrentDifficultyLevel,
                item.DaysCompleted,
                item.ExercisesCompleted,
                item.LastCompletionDate,
                item.IsActive,
                item.CompletedDate,
                item.AverageSuccessRate,
                item.CurrentStreak,
                item.LongestStreak))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DailyExerciseLogSummary>> GetDailyExerciseLogsAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted);

        if (dateFrom.HasValue)
        {
            query = query.Where(item => item.CompletedDate >= dateFrom);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(item => item.CompletedDate <= dateTo);
        }

        return await query
            .OrderByDescending(item => item.CompletedDate)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(item => new DailyExerciseLogSummary(
                item.Id,
                item.ExerciseId,
                item.ExerciseTypeId,
                item.DayNumber,
                item.WeekNumber,
                item.DifficultyLevel,
                item.CompletedDate,
                item.TimeSpentSeconds,
                item.SuccessRate,
                item.IsPassed,
                item.AttemptNumber,
                item.IsRetry,
                item.DevicePlatform,
                item.CorrectCount,
                item.IncorrectCount,
                item.TotalAttempts,
                item.AverageWPM,
                item.AverageComprehension))
            .ToListAsync(cancellationToken);
    }
}
