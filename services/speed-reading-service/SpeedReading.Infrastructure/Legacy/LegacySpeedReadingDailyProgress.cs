using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.DailyProgress;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingDailyProgress(SpeedReadingDbContext db) : ISpeedReadingDailyProgress
{
    private const decimal PassingScore = 70m;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<DailyExerciseSummary>> GetTodayExercisesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var program = await GetActiveProgramAsync(userId, cancellationToken);
        if (program is null)
        {
            return [];
        }

        var (week, day) = SpeedReadingDailyProgressRules.GetWeekAndDay(
            ((program.Value.Progress.CurrentWeek - 1) * 7) + program.Value.Progress.CurrentDay);
        return await BuildExercisesAsync(userId, program.Value.Progress, program.Value.Template, week, day, cancellationToken);
    }

    public async Task<IReadOnlyList<DailyExerciseSummary>> GetExercisesByDayAsync(
        Guid userId,
        int dayNumber,
        CancellationToken cancellationToken = default)
    {
        var (week, day) = SpeedReadingDailyProgressRules.GetWeekAndDay(dayNumber);
        var program = await GetActiveProgramAsync(userId, cancellationToken);
        if (program is null)
        {
            return [];
        }

        return await BuildExercisesAsync(userId, program.Value.Progress, program.Value.Template, week, day, cancellationToken);
    }

    public async Task<CompleteDailyExerciseResponse> CompleteExerciseAsync(
        Guid userId,
        CompleteDailyExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        _ = idempotencyKey;
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A valid authenticated user is required.", nameof(userId));
        }

        var score = SpeedReadingDailyProgressRules.ResolveScore(request.Score, request.SuccessRate);
        var duration = SpeedReadingDailyProgressRules.ResolveDuration(
            request.DurationSeconds,
            request.TimeSpentSeconds);
        var program = await GetActiveProgramAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("Aktif program bulunamadı.");

        var progress = program.Progress;
        var template = program.Template;
        var now = DateTime.UtcNow;
        var (week, day) = SpeedReadingDailyProgressRules.GetWeekAndDay(
            ((progress.CurrentWeek - 1) * 7) + progress.CurrentDay);

        LegacyDailyExerciseLog? log = null;
        if (request.ExerciseLogId.HasValue)
        {
            log = await db.DailyExerciseLogs
                .SingleOrDefaultAsync(item => item.Id == request.ExerciseLogId.Value
                    && item.UserId == userId
                    && item.StudentProgramProgressId == progress.Id
                    && !item.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Exercise log not found.");
        }

        var exerciseId = log?.ExerciseId ?? request.ExerciseId
            ?? throw new ArgumentException("ExerciseId or ExerciseLogId is required.", nameof(request));
        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise not found.");

        var wasPassed = log?.IsPassed == true;
        var oldDay = progress.CurrentDay;
        var oldWeek = progress.CurrentWeek;
        var oldDifficulty = progress.CurrentDifficultyLevel;
        var previousCompletionDate = progress.LastCompletionDate;

        if (log is null)
        {
            var attemptNumber = await db.DailyExerciseLogs
                .CountAsync(item => item.UserId == userId
                    && item.StudentProgramProgressId == progress.Id
                    && item.ExerciseId == exerciseId
                    && item.WeekNumber == week
                    && item.DayNumber == day
                    && !item.IsDeleted, cancellationToken) + 1;

            log = new LegacyDailyExerciseLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StudentProgramProgressId = progress.Id,
                ExerciseId = exerciseId,
                ExerciseTypeId = exercise.ExerciseTypeId,
                DayNumber = day,
                WeekNumber = week,
                DifficultyLevel = exercise.DifficultyLevel,
                AttemptNumber = attemptNumber,
                IsRetry = attemptNumber > 1,
                CreatedAt = now,
                CreatedBy = userId,
                IsDeleted = false
            };
            db.DailyExerciseLogs.Add(log);
        }

        log.CompletedDate = now;
        log.TimeSpentSeconds = duration;
        log.SuccessRate = score;
        log.IsPassed = score >= PassingScore;
        log.ResultDataJson = string.IsNullOrWhiteSpace(request.ResultDataJson) ? "{}" : request.ResultDataJson;
        log.DevicePlatform = string.IsNullOrWhiteSpace(request.DevicePlatform) ? "web-desktop" : request.DevicePlatform.Trim();
        log.CorrectCount = Math.Max(request.CorrectCount, 0);
        log.IncorrectCount = Math.Max(request.IncorrectCount, 0);
        log.TotalAttempts = request.TotalAttempts > 0
            ? request.TotalAttempts
            : log.CorrectCount + log.IncorrectCount;
        log.AverageResponseTimeMs = Math.Max(request.AverageResponseTimeMs, 0);
        log.MedianResponseTimeMs = Math.Max(request.MedianResponseTimeMs, 0);
        log.StdDevResponseTimeMs = Math.Max(request.StdDevResponseTimeMs, 0);
        log.PauseCount = Math.Max(request.PauseCount, 0);
        log.TotalPausedSeconds = Math.Max(request.TotalPausedSeconds, 0);
        log.DayOfWeek = (int)now.DayOfWeek;
        log.TimeOfDay = now.TimeOfDay;
        log.UpdatedAt = now;
        log.UpdatedBy = userId;

        if (!wasPassed)
        {
            progress.ExercisesCompleted++;
        }

        progress.LastCompletionDate = now;
        progress.UpdatedAt = now;
        progress.UpdatedBy = userId;

        var allLogs = await db.DailyExerciseLogs
            .Where(item => item.StudentProgramProgressId == progress.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (log.EntityStateIsAdded(db))
        {
            allLogs.Add(log);
        }

        progress.AverageSuccessRate = allLogs.Count == 0
            ? score
            : allLogs.Average(item => item.SuccessRate);

        var expectedCount = await CountExpectedExercisesAsync(
            progress,
            template,
            week,
            day,
            cancellationToken);
        var completedCount = allLogs.Count(item => item.WeekNumber == week && item.DayNumber == day);
        var dayCompleted = expectedCount > 0 && completedCount >= expectedCount;
        var weekChanged = false;
        var difficultyIncreased = false;
        var programCompleted = false;

        if (dayCompleted && oldDay == progress.CurrentDay && oldWeek == progress.CurrentWeek)
        {
            progress.DaysCompleted++;
            progress.CurrentStreak = SpeedReadingDailyProgressRules.CalculateNextStreak(
                previousCompletionDate,
                now,
                progress.CurrentStreak);
            progress.LongestStreak = Math.Max(progress.LongestStreak, progress.CurrentStreak);

            var completedCumulativeDay = ((progress.CurrentWeek - 1) * 7) + progress.CurrentDay;
            progress.CurrentDay++;

            if (template.TotalDays > 0 && completedCumulativeDay >= template.TotalDays)
            {
                programCompleted = true;
                progress.CompletedDate = now;
                progress.IsActive = false;
            }
            else if (progress.CurrentDay > 7)
            {
                progress.CurrentWeek++;
                progress.CurrentDay = 1;
                weekChanged = true;

                if (template.WeeksPerDifficultyIncrease > 0
                    && progress.CurrentWeek % template.WeeksPerDifficultyIncrease == 0
                    && progress.CurrentDifficultyLevel < template.MaxDifficultyLevel)
                {
                    progress.CurrentDifficultyLevel++;
                    difficultyIncreased = true;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new CompleteDailyExerciseResponse(
            true,
            programCompleted ? "Program başarıyla tamamlandı." : "Egzersiz başarıyla tamamlandı.",
            dayCompleted,
            progress.CurrentDay,
            progress.CurrentWeek,
            progress.CurrentDifficultyLevel,
            difficultyIncreased,
            oldDifficulty,
            weekChanged,
            oldWeek,
            progress.CurrentDifficultyLevel,
            progress.CurrentStreak,
            progress.LongestStreak,
            programCompleted,
            null,
            programCompleted
                ? new ProgramCompletionStats(
                    progress.DaysCompleted,
                    progress.AverageSuccessRate,
                    progress.LongestStreak,
                    progress.ExercisesCompleted)
                : null);
    }

    public async Task<DailyProgressSummary?> GetProgressSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var program = await GetActiveOrLatestProgramAsync(userId, cancellationToken);
        if (program is null)
        {
            return null;
        }

        var logs = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var completed = logs.Where(item => item.IsPassed).ToList();
        var results = await db.StudentExerciseResults
            .AsNoTracking()
            .Where(item => item.StudentId == userId && !item.IsDeleted && item.RawWPM > 0)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.RawWPM, item.ComprehensionScore })
            .ToListAsync(cancellationToken);

        return new DailyProgressSummary(
            program.Value.Progress.Id,
            program.Value.Progress.CurrentDay,
            program.Value.Progress.DaysCompleted,
            program.Value.Progress.ExercisesCompleted,
            logs.Count,
            completed.Count,
            program.Value.Progress.AssignedDate,
            results.Count == 0 ? 0 : results.TakeLast(5).Average(item => item.RawWPM),
            results.Count == 0 ? 0 : results.Take(5).Average(item => item.RawWPM),
            results.Count == 0 ? 0 : results.Average(item => item.ComprehensionScore));
    }

    public async Task<WeeklyProgressSummary> GetWeeklyStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var logs = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && !item.IsDeleted
                && item.CompletedDate >= weekStart
                && item.CompletedDate < weekStart.AddDays(7))
            .ToListAsync(cancellationToken);

        return new WeeklyProgressSummary(
            logs.Count,
            logs.Count(item => item.IsPassed),
            logs.Where(item => item.IsPassed).Select(item => item.SuccessRate).DefaultIfEmpty().Average(),
            logs.Sum(item => item.TimeSpentSeconds) / 60,
            logs.Select(item => item.CompletedDate.Date).Distinct().Count());
    }

    public async Task<DailyProgressCalendar> GetCalendarAsync(
        Guid userId,
        int? month,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var targetMonth = month ?? DateTime.UtcNow.Month;
        var targetYear = year ?? DateTime.UtcNow.Year;
        if (targetMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        if (targetYear is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        var firstDay = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = firstDay.AddMonths(1);
        var logs = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && !item.IsDeleted
                && item.CompletedDate >= firstDay
                && item.CompletedDate < nextMonth)
            .ToListAsync(cancellationToken);

        var days = logs
            .GroupBy(item => item.CompletedDate.Date)
            .OrderBy(group => group.Key)
            .Select(group => new DailyProgressCalendarDay(
                group.Key,
                group.Count(),
                group.Count(item => item.IsPassed),
                group.Where(item => item.IsPassed).Select(item => item.SuccessRate).DefaultIfEmpty().Average()))
            .ToList();

        return new DailyProgressCalendar(targetMonth, targetYear, days);
    }

    private async Task<(LegacyStudentProgramProgress Progress, LegacyExerciseProgramTemplate Template)?> GetActiveProgramAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var progress = await db.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive && item.CompletedDate == null && !item.IsDeleted)
            .OrderByDescending(item => item.AssignedDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (progress is null)
        {
            return null;
        }

        var template = await db.ExerciseProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == progress.ProgramTemplateId && !item.IsDeleted, cancellationToken);
        return template is null ? null : (progress, template);
    }

    private async Task<(LegacyStudentProgramProgress Progress, LegacyExerciseProgramTemplate Template)?> GetActiveOrLatestProgramAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var progress = await db.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.AssignedDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (progress is null)
        {
            return null;
        }

        var template = await db.ExerciseProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == progress.ProgramTemplateId && !item.IsDeleted, cancellationToken);
        return template is null ? null : (progress, template);
    }

    private async Task<List<DailyExerciseSummary>> BuildExercisesAsync(
        Guid userId,
        LegacyStudentProgramProgress progress,
        LegacyExerciseProgramTemplate template,
        int week,
        int day,
        CancellationToken cancellationToken)
    {
        var patterns = GetPatterns(template.WeeklyPatternJson, week, day);
        if (patterns.Count == 0)
        {
            return [];
        }

        var completedLogs = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && item.StudentProgramProgressId == progress.Id
                && item.WeekNumber == week
                && item.DayNumber == day
                && !item.IsDeleted)
            .OrderBy(item => item.CompletedDate)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var completedByExercise = completedLogs
            .GroupBy(item => item.ExerciseId)
            .ToDictionary(group => group.Key, group => new Queue<LegacyDailyExerciseLog>(group));

        var result = new List<DailyExerciseSummary>();
        var order = 1;
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern.Type) || pattern.Count <= 0)
            {
                continue;
            }

            var exerciseType = await db.ExerciseTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Name == pattern.Type && !item.IsDeleted, cancellationToken);
            if (exerciseType is null)
            {
                continue;
            }

            var candidates = await FindCandidatesAsync(exerciseType.Id, pattern.Difficulty, template, pattern.Count, cancellationToken);
            foreach (var exercise in candidates)
            {
                completedByExercise.TryGetValue(exercise.Id, out var queue);
                var completed = queue is not null && queue.Count > 0 ? queue.Dequeue() : null;
                result.Add(new DailyExerciseSummary(
                    exercise.Id,
                    exerciseType.Id,
                    exerciseType.Name,
                    exercise.Title,
                    exercise.Description,
                    exercise.DifficultyLevel,
                    5,
                    completed is not null,
                    completed?.CompletedDate,
                    order++,
                    exercise.ConfigurationJson));
            }
        }

        return result;
    }

    private async Task<int> CountExpectedExercisesAsync(
        LegacyStudentProgramProgress progress,
        LegacyExerciseProgramTemplate template,
        int week,
        int day,
        CancellationToken cancellationToken)
    {
        var patterns = GetPatterns(template.WeeklyPatternJson, week, day);
        var count = 0;
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern.Type) || pattern.Count <= 0)
            {
                continue;
            }

            var typeId = await db.ExerciseTypes
                .AsNoTracking()
                .Where(item => item.Name == pattern.Type && !item.IsDeleted)
                .Select(item => (Guid?)item.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (!typeId.HasValue)
            {
                continue;
            }

            var candidates = await FindCandidatesAsync(typeId.Value, pattern.Difficulty, template, pattern.Count, cancellationToken);
            count += candidates.Count;
        }

        return count;
    }

    private async Task<List<LegacyExercise>> FindCandidatesAsync(
        Guid exerciseTypeId,
        int difficulty,
        LegacyExerciseProgramTemplate template,
        int requestedCount,
        CancellationToken cancellationToken)
    {
        var candidates = await db.Exercises
            .AsNoTracking()
            .Where(item => item.ExerciseTypeId == exerciseTypeId
                && item.DifficultyLevel == difficulty
                && (item.TargetAgeGroupConfigurationId == null
                    || item.TargetAgeGroupConfigurationId == template.TargetAgeGroupConfigurationId)
                && !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            for (var fallbackDifficulty = difficulty - 1; fallbackDifficulty >= 1 && candidates.Count == 0; fallbackDifficulty--)
            {
                candidates = await db.Exercises
                    .AsNoTracking()
                    .Where(item => item.ExerciseTypeId == exerciseTypeId
                        && item.DifficultyLevel == fallbackDifficulty
                        && (item.TargetAgeGroupConfigurationId == null
                            || item.TargetAgeGroupConfigurationId == template.TargetAgeGroupConfigurationId)
                        && !item.IsDeleted)
                    .OrderBy(item => item.Id)
                    .ToListAsync(cancellationToken);
            }
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        return Enumerable.Range(0, requestedCount)
            .Select(index => candidates[index % candidates.Count])
            .ToList();
    }

    private static List<ExercisePattern> GetPatterns(string json, int week, int day)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var daily = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<ExercisePattern>>>>(json, JsonOptions);
            if (daily is not null && daily.Count > 0)
            {
                var weekData = daily.GetValueOrDefault($"week{week}")
                    ?? daily.GetValueOrDefault("week1");
                if (weekData is null)
                {
                    return [];
                }

                return weekData.GetValueOrDefault($"day{day}")
                    ?? weekData.GetValueOrDefault("day1")
                    ?? [];
            }
        }
        catch (JsonException)
        {
            // Try the original one-level pattern below.
        }

        try
        {
            var legacy = JsonSerializer.Deserialize<Dictionary<string, List<ExercisePattern>>>(json, JsonOptions);
            return legacy?.GetValueOrDefault($"week{week}")
                ?? legacy?.GetValueOrDefault("week1")
                ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class ExercisePattern
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Difficulty { get; set; }
    }
}

internal static class LegacyDailyExerciseLogExtensions
{
    public static bool EntityStateIsAdded(this LegacyDailyExerciseLog entity, SpeedReadingDbContext db) =>
        db.Entry(entity).State == EntityState.Added;
}
