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
    ISpeedReadingUserDirectory userDirectory,
    ISpeedReadingProgressAccess progressAccess) : ILegacySpeedReadingPrograms
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
        SpeedReadingProgressAccessScope accessScope,
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
            join profile in db.UserProfiles.AsNoTracking()
                on progress.UserId equals profile.UserId into profileRows
            from profile in profileRows.DefaultIfEmpty()
            where template == null || !template.IsDeleted
            select new
            {
                Progress = progress,
                TemplateName = template == null ? string.Empty : template.Name,
                HistoricalDisplayName = profile == null ? null : profile.HistoricalDisplayName,
                HistoricalEmail = profile == null ? null : profile.HistoricalEmail
            };

        if (!accessScope.IsGlobal)
        {
            var allowedStudentIds = accessScope.StudentUserIds.Distinct().ToArray();
            if (allowedStudentIds.Length == 0)
                return new SpeedReadingPage<AdminStudentProgressSummary>([], page, size, 0);
            query = query.Where(row => allowedStudentIds.Contains(row.Progress.UserId));
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            var totalCount = await query.CountAsync(cancellationToken);
            var pageRows = await query
                .OrderByDescending(row => row.Progress.CreatedAt)
                .ThenByDescending(row => row.Progress.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            var pageUsers = await userDirectory.GetUsersAsync(
                pageRows.Select(row => row.Progress.UserId).Distinct().ToArray(),
                cancellationToken);
            var pageUsersById = pageUsers.Users.ToDictionary(item => item.UserId);
            var items = pageRows
                .Select(row => ToAdminProgressSummary(row.Progress, row.TemplateName, row.HistoricalDisplayName, row.HistoricalEmail, pageUsersById))
                .ToList();

            return new SpeedReadingPage<AdminStudentProgressSummary>(items, page, size, totalCount);
        }

        var normalizedSearch = searchTerm.Trim().ToLowerInvariant();
        var searchId = SpeedReadingAdminProgressSearch.TryParseId(searchTerm);
        var matchingUserIds = searchId.HasValue || normalizedSearch.Length < 2
            ? Array.Empty<Guid>()
            : await progressAccess.SearchStudentUserIdsAsync(
                accessScope.ViewerUserId,
                normalizedSearch,
                cancellationToken);

        query = query.Where(row =>
            (searchId.HasValue && (row.Progress.Id == searchId.Value
                || row.Progress.UserId == searchId.Value
                || row.Progress.ProgramTemplateId == searchId.Value))
            || row.TemplateName.ToLower().Contains(normalizedSearch)
            || matchingUserIds.Contains(row.Progress.UserId));

        var searchTotalCount = await query.CountAsync(cancellationToken);
        var searchPageRows = await query
            .OrderByDescending(row => row.Progress.CreatedAt)
            .ThenByDescending(row => row.Progress.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        var searchPageUsers = await userDirectory.GetUsersAsync(
            searchPageRows.Select(row => row.Progress.UserId).Distinct().ToArray(),
            cancellationToken);
        var searchPageUsersById = searchPageUsers.Users.ToDictionary(item => item.UserId);
        var filteredItems = searchPageRows
            .Select(row => ToAdminProgressSummary(row.Progress, row.TemplateName, row.HistoricalDisplayName, row.HistoricalEmail, searchPageUsersById))
            .ToList();

        return new SpeedReadingPage<AdminStudentProgressSummary>(
            filteredItems,
            page,
            size,
            searchTotalCount);
    }

    public async Task<AdminStudentProgressDetails?> GetAdminStudentProgressDetailsAsync(
        SpeedReadingProgressAccessScope accessScope,
        Guid progressId,
        CancellationToken cancellationToken = default)
    {
        var progress = await db.StudentProgramProgresses
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == progressId
                && (accessScope.IsGlobal || accessScope.StudentUserIds.Contains(item.UserId)), cancellationToken);
        if (progress is null)
            return null;

        var recentLogs = await (
                from log in db.DailyExerciseLogs.AsNoTracking()
                join exercise in db.Exercises.AsNoTracking() on log.ExerciseId equals exercise.Id into exerciseRows
                from exercise in exerciseRows.DefaultIfEmpty()
                join exerciseType in db.ExerciseTypes.AsNoTracking() on log.ExerciseTypeId equals exerciseType.Id into exerciseTypeRows
                from exerciseType in exerciseTypeRows.DefaultIfEmpty()
                join sessionResult in db.ExerciseSessionResults.AsNoTracking() on log.SessionId equals sessionResult.SessionId into sessionResultRows
                from sessionResult in sessionResultRows.DefaultIfEmpty()
                where log.StudentProgramProgressId == progress.Id
                orderby log.CompletedDate descending
                select new DailyExerciseLogSummary(
                    log.Id,
                    log.ExerciseId,
                    log.ExerciseTypeId,
                    log.DayNumber,
                    log.WeekNumber,
                    log.DifficultyLevel,
                    log.CompletedDate,
                    log.TimeSpentSeconds,
                    log.IsMeasured ? log.SuccessRate : null,
                    log.IsPassed,
                    log.AttemptNumber,
                    log.IsRetry,
                    log.DevicePlatform,
                    log.CorrectCount,
                    log.IncorrectCount,
                    log.TotalAttempts,
                    sessionResult != null && sessionResult.IsMeasured && sessionResult.RawWpm > 0
                        ? sessionResult.RawWpm
                        : log.AverageWPM,
                    sessionResult != null && sessionResult.IsMeasured
                        ? sessionResult.ComprehensionScore
                        : log.AverageComprehension,
                    log.IsMeasured ? "Measured" : "NotMeasured",
                    exercise == null ? null : exercise.Title,
                    exerciseType == null ? null : exerciseType.DisplayName))
            .Take(30)
            .ToListAsync(cancellationToken);

        return new AdminStudentProgressDetails(ToProgressSummary(progress), recentLogs);
    }

    public async Task<bool> ResetStudentProgressAsync(
        SpeedReadingProgressAccessScope accessScope,
        Guid progressId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var progress = await db.StudentProgramProgresses
            .SingleOrDefaultAsync(item => item.Id == progressId
                && (accessScope.IsGlobal || accessScope.StudentUserIds.Contains(item.UserId)), cancellationToken);
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

    private static AdminStudentProgressSummary ToAdminProgressSummary(
        StudentProgramProgress progress,
        string templateName,
        string? historicalDisplayName,
        string? historicalEmail,
        IReadOnlyDictionary<Guid, EduPlatform.Shared.Contracts.Reporting.SpeedReadingUserDirectoryItem> usersById)
    {
        usersById.TryGetValue(progress.UserId, out var user);
        var studentName = user is null ? null : $"{user.FirstName} {user.LastName}".Trim();
        return new AdminStudentProgressSummary(
            progress.Id,
            progress.UserId,
            progress.ProgramTemplateId,
            progress.CurrentDay,
            progress.DaysCompleted,
            progress.ExercisesCompleted,
            progress.AssignedDate,
            string.IsNullOrWhiteSpace(studentName) ? historicalDisplayName : studentName,
            string.IsNullOrWhiteSpace(user?.Email) ? historicalEmail : user.Email,
            templateName);
    }

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
            item.IsMeasured ? "Measured" : "NotMeasured",
            null,
            null);

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
            item.IsMeasured ? "Measured" : "NotMeasured",
            null,
            null);

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
