using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using SpeedReading.Domain.Programs;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Program templates, student progress and daily logs backed only by the
/// owned Speed Reading store. User profile search is resolved through the
/// Identity service, never through the legacy database.
/// </summary>
internal sealed class OwnedSpeedReadingPrograms(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingUserDirectory userDirectory) : ILegacySpeedReadingPrograms
{
    public async Task<IReadOnlyList<ExerciseProgramTemplateSummary>> GetProgramTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        await db.ProgramTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Select(ToTemplateSummary())
            .ToListAsync(cancellationToken);

    public async Task<ExerciseProgramTemplateAdminSummary?> GetProgramTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var item = await db.ProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(template => template.Id == templateId && !template.IsDeleted, cancellationToken);
        return item is null ? null : ToAdminSummary(item);
    }

    public async Task<IReadOnlyList<ExerciseProgramTemplateAdminSummary>> GetProgramTemplateAdminSummariesAsync(
        CancellationToken cancellationToken = default) =>
        await db.ProgramTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Select(ToAdminSummary())
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentProgramProgressSummary>> GetStudentProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await db.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.AssignedDate)
            .Select(ToProgressSummary())
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
            join template in db.ProgramTemplates.AsNoTracking()
                on progress.ProgramTemplateId equals template.Id into templateRows
            from template in templateRows.DefaultIfEmpty()
            where template == null || !template.IsDeleted
            select new
            {
                Progress = progress,
                TemplateName = template == null ? string.Empty : template.Name
            };

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
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

        var normalizedSearch = searchTerm.Trim();
        var rows = await query
            .OrderByDescending(row => row.Progress.CreatedAt)
            .ThenByDescending(row => row.Progress.Id)
            .ToListAsync(cancellationToken);
        var users = await userDirectory.GetUsersAsync(
            rows.Select(row => row.Progress.UserId).Distinct().ToArray(),
            cancellationToken);
        var usersById = users.Users.ToDictionary(item => item.UserId);
        var filteredRows = rows
            .Where(row => row.TemplateName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || (usersById.TryGetValue(row.Progress.UserId, out var user)
                    && ($"{user.FirstName} {user.LastName}".Contains(
                            normalizedSearch,
                            StringComparison.OrdinalIgnoreCase)
                        || (user.Email?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false))))
            .ToList();

        var filteredItems = filteredRows
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
            .ToList();

        return new SpeedReadingPage<AdminStudentProgressSummary>(
            filteredItems,
            page,
            size,
            filteredRows.Count);
    }

    public async Task<AdminStudentProgressDetails?> GetAdminStudentProgressDetailsAsync(
        Guid progressId,
        CancellationToken cancellationToken = default)
    {
        var progress = await db.StudentProgramProgresses
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == progressId, cancellationToken);
        if (progress is null)
            return null;

        var recentLogs = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == progress.UserId)
            .OrderByDescending(item => item.CompletedDate)
            .Take(30)
            .Select(ToDailyLogSummary())
            .ToListAsync(cancellationToken);

        return new AdminStudentProgressDetails(ToProgressSummary(progress), recentLogs);
    }

    public async Task<bool> ResetStudentProgressAsync(
        Guid progressId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var progress = await db.StudentProgramProgresses
            .SingleOrDefaultAsync(item => item.Id == progressId, cancellationToken);
        if (progress is null)
            return false;

        progress.Reset(actorId, DateTime.UtcNow);
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
            .Where(item => item.UserId == userId);

        if (dateFrom.HasValue)
            query = query.Where(item => item.CompletedDate >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(item => item.CompletedDate <= dateTo.Value);

        return await query
            .OrderByDescending(item => item.CompletedDate)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(ToDailyLogSummary())
            .ToListAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<ProgramTemplate, ExerciseProgramTemplateSummary>> ToTemplateSummary() =>
        item => new ExerciseProgramTemplateSummary(
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
            item.IsAssessment);

    private static System.Linq.Expressions.Expression<Func<ProgramTemplate, ExerciseProgramTemplateAdminSummary>> ToAdminSummary() =>
        item => new ExerciseProgramTemplateAdminSummary(
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

    private static System.Linq.Expressions.Expression<Func<StudentProgramProgress, StudentProgramProgressSummary>> ToProgressSummary() =>
        item => new StudentProgramProgressSummary(
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
            item.LongestStreak);

    private static StudentProgramProgressSummary ToProgressSummary(StudentProgramProgress item) =>
        new(
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
            item.LongestStreak);

    private static System.Linq.Expressions.Expression<Func<DailyExerciseLog, DailyExerciseLogSummary>> ToDailyLogSummary() =>
        item => new DailyExerciseLogSummary(
            item.Id,
            item.ExerciseId,
            item.ExerciseTypeId,
            item.DayNumber,
            item.WeekNumber,
            item.DifficultyLevel,
            item.CompletedDate,
            item.TimeSpentSeconds,
            item.IsMeasured ? item.SuccessRate : null,
            item.IsPassed,
            item.AttemptNumber,
            item.IsRetry,
            item.DevicePlatform,
            item.CorrectCount,
            item.IncorrectCount,
            item.TotalAttempts,
            item.AverageWPM,
            item.AverageComprehension,
            item.IsMeasured ? "Measured" : "NotMeasured");

    private static DailyExerciseLogSummary ToDailyLogSummary(DailyExerciseLog item) =>
        new(
            item.Id,
            item.ExerciseId,
            item.ExerciseTypeId,
            item.DayNumber,
            item.WeekNumber,
            item.DifficultyLevel,
            item.CompletedDate,
            item.TimeSpentSeconds,
            item.IsMeasured ? item.SuccessRate : null,
            item.IsPassed,
            item.AttemptNumber,
            item.IsRetry,
            item.DevicePlatform,
            item.CorrectCount,
            item.IncorrectCount,
            item.TotalAttempts,
            item.AverageWPM,
            item.AverageComprehension,
            item.IsMeasured ? "Measured" : "NotMeasured");

    private static ExerciseProgramTemplateAdminSummary ToAdminSummary(ProgramTemplate item) =>
        new(
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

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));
}
