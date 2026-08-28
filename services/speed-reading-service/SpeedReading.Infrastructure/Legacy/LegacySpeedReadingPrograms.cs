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

    public async Task<ExerciseProgramTemplateAdminSummary?> GetProgramTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var item = await db.ExerciseProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(template => template.Id == templateId && !template.IsDeleted, cancellationToken);
        return item is null
            ? null
            : new ExerciseProgramTemplateAdminSummary(
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
                item.IsAssessment);
    }

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

    public async Task<SpeedReadingPage<AdminStudentProgressSummary>> GetAdminStudentProgressAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query =
            from progress in db.StudentProgramProgresses.AsNoTracking()
            join user in db.Users.AsNoTracking()
                on progress.UserId equals user.Id into userRows
            from user in userRows.DefaultIfEmpty()
            join template in db.ExerciseProgramTemplates.AsNoTracking()
                on progress.ProgramTemplateId equals template.Id into templateRows
            from template in templateRows.DefaultIfEmpty()
            where !progress.IsDeleted
            select new { Progress = progress, User = user, Template = template };

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearch = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(row =>
                (row.User != null
                    && ((row.User.FirstName + " " + row.User.LastName).ToLower().Contains(normalizedSearch)
                        || (row.User.Email ?? string.Empty).ToLower().Contains(normalizedSearch)))
                || (row.Template != null && row.Template.Name.ToLower().Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(row => row.Progress.CreatedAt)
            .ThenByDescending(row => row.Progress.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(row => new AdminStudentProgressSummary(
                row.Progress.Id,
                row.Progress.UserId,
                row.Progress.ProgramTemplateId,
                row.Progress.CurrentDay,
                row.Progress.DaysCompleted,
                row.Progress.ExercisesCompleted,
                row.Progress.AssignedDate))
            .ToListAsync(cancellationToken);

        return new SpeedReadingPage<AdminStudentProgressSummary>(items, page, size, totalCount);
    }

    public async Task<AdminStudentProgressDetails?> GetAdminStudentProgressDetailsAsync(
        Guid progressId,
        CancellationToken cancellationToken = default)
    {
        var progress = await db.StudentProgramProgresses
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == progressId && !item.IsDeleted, cancellationToken);
        if (progress is null)
        {
            return null;
        }

        var recentLogs = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == progress.UserId && !item.IsDeleted)
            .OrderByDescending(item => item.CompletedDate)
            .Take(30)
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
                item.AverageComprehension,
                "Measured"))
            .ToListAsync(cancellationToken);

        return new AdminStudentProgressDetails(
            new StudentProgramProgressSummary(
                progress.Id,
                progress.ProgramTemplateId,
                progress.AssignedDate,
                progress.CurrentDay,
                progress.CurrentWeek,
                progress.CurrentDifficultyLevel,
                progress.DaysCompleted,
                progress.ExercisesCompleted,
                progress.LastCompletionDate,
                progress.IsActive,
                progress.CompletedDate,
                progress.AverageSuccessRate,
                progress.CurrentStreak,
                progress.LongestStreak),
            recentLogs);
    }

    public async Task<bool> ResetStudentProgressAsync(
        Guid progressId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var progress = await db.StudentProgramProgresses
            .SingleOrDefaultAsync(item => item.Id == progressId && !item.IsDeleted, cancellationToken);
        if (progress is null)
        {
            return false;
        }

        progress.CurrentDay = 1;
        progress.DaysCompleted = 0;
        progress.ExercisesCompleted = 0;
        progress.AverageSuccessRate = 0;
        progress.UpdatedAt = DateTime.UtcNow;
        progress.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

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
                item.AverageComprehension,
                "Measured"))
            .ToListAsync(cancellationToken);
    }

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));
}
