using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.Progress;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Daily exercise selection and completion backed only by owned Speed Reading
/// data. Reading-speed history is read from the owned session-result store.
/// </summary>
internal sealed class OwnedSpeedReadingDailyProgress(OwnedSpeedReadingDbContext db)
    : ISpeedReadingDailyProgress
{
    private const decimal PassingScore = 70m;
    private const string IdempotencyScope = "speed-reading.daily-progress.complete-exercise";
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
            return [];

        var (week, day) = SpeedReadingDailyProgressRules.GetWeekAndDay(
            ((program.Value.Progress.CurrentWeek - 1) * 7) + program.Value.Progress.CurrentDay);
        return await BuildExercisesAsync(
            userId,
            program.Value.Progress,
            program.Value.Template,
            week,
            day,
            cancellationToken);
    }

    public async Task<IReadOnlyList<DailyExerciseSummary>> GetExercisesByDayAsync(
        Guid userId,
        int dayNumber,
        CancellationToken cancellationToken = default)
    {
        var (week, day) = SpeedReadingDailyProgressRules.GetWeekAndDay(dayNumber);
        var program = await GetActiveProgramAsync(userId, cancellationToken);
        if (program is null)
            return [];

        return await BuildExercisesAsync(
            userId,
            program.Value.Progress,
            program.Value.Template,
            week,
            day,
            cancellationToken);
    }

    public async Task<CompleteDailyExerciseResponse> CompleteExerciseAsync(
        Guid userId,
        CompleteDailyExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid authenticated user is required.", nameof(userId));

        idempotencyKey = SpeedReadingDailyProgressRules.ValidateIdempotencyKey(idempotencyKey);
        var requestHash = CreateCompletionHash(userId, request);
        var existing = await db.IdempotencyRecords
            .SingleOrDefaultAsync(
                item => item.Scope == IdempotencyScope && item.Key == idempotencyKey,
                cancellationToken);
        if (existing is not null)
            return await ReplayAsync(existing, userId, requestHash, cancellationToken);

        var duration = SpeedReadingDailyProgressRules.ResolveDuration(
            request.DurationSeconds,
            request.TimeSpentSeconds);
        var program = await GetActiveProgramAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("Aktif program bulunamadı.");

        var progress = program.Progress;
        var template = program.Template;
        db.StudentProgramProgresses.Attach(progress);
        var now = DateTime.UtcNow;
        var (week, day) = SpeedReadingDailyProgressRules.GetWeekAndDay(
            ((progress.CurrentWeek - 1) * 7) + progress.CurrentDay);

        DailyExerciseLog? log = null;
        if (request.ExerciseLogId.HasValue)
        {
            log = await db.DailyExerciseLogs
                .SingleOrDefaultAsync(item => item.Id == request.ExerciseLogId.Value
                    && item.UserId == userId
                    && item.StudentProgramProgressId == progress.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Exercise log not found.");
        }

        var exerciseId = log?.ExerciseId ?? request.ExerciseId
            ?? throw new ArgumentException("ExerciseId or ExerciseLogId is required.", nameof(request));
        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exerciseId && item.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise not found.");

        var sessionResult = request.SessionId.HasValue
            ? await db.ExerciseSessionResults
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.SessionId == request.SessionId.Value
                    && item.StudentId == userId
                    && item.ExerciseId == exerciseId, cancellationToken)
            : null;
        if (request.SessionId.HasValue && sessionResult is null)
        {
            throw new KeyNotFoundException("Completed exercise session result not found.");
        }

        var isMeasured = sessionResult?.IsMeasured
            ?? !string.Equals(request.MeasurementStatus, "NotMeasured", StringComparison.OrdinalIgnoreCase);
        var score = isMeasured
            ? sessionResult?.Score ?? SpeedReadingDailyProgressRules.ResolveScore(request.Score, request.SuccessRate)
            : 0;

        var wasPreviouslyPassed = log?.IsPassed == true;
        if (log is null)
        {
            var attemptNumber = await db.DailyExerciseLogs
                .CountAsync(item => item.UserId == userId
                    && item.StudentProgramProgressId == progress.Id
                    && item.ExerciseId == exerciseId
                    && item.WeekNumber == week
                    && item.DayNumber == day, cancellationToken) + 1;

            log = DailyExerciseLog.Import(
                Guid.NewGuid(),
                userId,
                progress.Id,
                exerciseId,
                exercise.ExerciseTypeId,
                day,
                week,
                exercise.DifficultyLevel,
                now,
                duration,
                score,
                !isMeasured || score >= PassingScore,
                "{}",
                attemptNumber,
                attemptNumber > 1,
                "web-desktop",
                0,
                0,
                0,
                null,
                null,
                (int)now.DayOfWeek,
                now.TimeOfDay,
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                now,
                userId.ToString(),
                now,
                userId.ToString(),
                isMeasured);
            db.DailyExerciseLogs.Add(log);
        }

        log.Complete(
            now,
            duration,
            score,
            score >= PassingScore,
            request.ResultDataJson,
            request.DevicePlatform,
            request.CorrectCount,
            request.IncorrectCount,
            request.TotalAttempts,
            request.AverageResponseTimeMs,
            request.MedianResponseTimeMs,
            request.StdDevResponseTimeMs,
            request.PauseCount,
            request.TotalPausedSeconds,
            userId,
            isMeasured);

        var allLogs = await db.DailyExerciseLogs
            .Where(item => item.StudentProgramProgressId == progress.Id)
            .ToListAsync(cancellationToken);
        if (!allLogs.Contains(log))
            allLogs.Add(log);

        var measuredScores = allLogs
            .Where(item => item.IsMeasured)
            .Select(item => item.SuccessRate)
            .ToList();
        var averageSuccessRate = measuredScores.Count > 0
            ? measuredScores.Average()
            : progress.AverageSuccessRate;
        var expectedCount = await CountExpectedExercisesAsync(
            progress,
            template,
            week,
            day,
            cancellationToken);
        var completedCount = allLogs.Count(item => item.WeekNumber == week && item.DayNumber == day);
        var completion = progress.ApplyExerciseCompletion(
            averageSuccessRate,
            wasPreviouslyPassed,
            completedCount,
            expectedCount,
            template,
            userId,
            now);

        db.IdempotencyRecords.Add(new OwnedIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = IdempotencyScope,
            Key = idempotencyKey,
            RequestHash = requestHash,
            ResourceId = log.Id,
            CreatedAt = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords
                .SingleAsync(
                    item => item.Scope == IdempotencyScope && item.Key == idempotencyKey,
                    cancellationToken);
            return await ReplayAsync(concurrent, userId, requestHash, cancellationToken);
        }

        return new CompleteDailyExerciseResponse(
            true,
            completion.ProgramCompleted ? "Program başarıyla tamamlandı." : "Egzersiz başarıyla tamamlandı.",
            completion.DayCompleted,
            progress.CurrentDay,
            progress.CurrentWeek,
            progress.CurrentDifficultyLevel,
            completion.DifficultyIncreased,
            completion.OldDifficultyLevel,
            completion.WeekChanged,
            completion.OldWeek,
            progress.CurrentDifficultyLevel,
            progress.CurrentStreak,
            progress.LongestStreak,
            completion.ProgramCompleted,
            null,
            completion.ProgramCompleted
                ? new ProgramCompletionStats(
                    progress.DaysCompleted,
                    progress.AverageSuccessRate,
                    progress.LongestStreak,
                    progress.ExercisesCompleted)
            : null);
    }

    private async Task<CompleteDailyExerciseResponse> ReplayAsync(
        OwnedIdempotencyRecord record,
        Guid userId,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if (!record.Matches(requestHash))
        {
            throw new BusinessRuleException(
                "Idempotency.Conflict",
                "The idempotency key was already used with a different request payload.");
        }

        var progressId = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.Id == record.ResourceId && item.UserId == userId)
            .Select(item => (Guid?)item.StudentProgramProgressId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!progressId.HasValue)
        {
            throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "The idempotency record points to a missing daily exercise log.");
        }

        var progress = await db.StudentProgramProgresses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.UserId == userId && item.Id == progressId.Value,
                cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ProgressMissing",
                "The idempotent daily completion progress record is missing.");

        return new CompleteDailyExerciseResponse(
            true,
            "Egzersiz daha önce tamamlandı.",
            false,
            progress.CurrentDay,
            progress.CurrentWeek,
            progress.CurrentDifficultyLevel,
            false,
            progress.CurrentDifficultyLevel,
            false,
            progress.CurrentWeek,
            progress.CurrentDifficultyLevel,
            progress.CurrentStreak,
            progress.LongestStreak,
            progress.CompletedDate.HasValue,
            null,
            null);
    }

    private static string CreateCompletionHash(Guid userId, CompleteDailyExerciseRequest request) =>
        SpeedReadingRequestHasher.Create(
            userId.ToString("D"),
            IdempotencyScope,
            JsonSerializer.Serialize(request, JsonOptions));

    private static bool IsIdempotencyConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && string.Equals(
            postgres.ConstraintName,
            "ix_idempotency_records_scope_key",
            StringComparison.Ordinal);

    public async Task<DailyProgressSummary?> GetProgressSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var program = await GetActiveOrLatestProgramAsync(userId, cancellationToken);
        if (program is null)
            return null;

        var logs = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);
        var completed = logs.Where(item => item.IsPassed).ToList();
        var results = await db.ExerciseSessionResults
            .AsNoTracking()
            .Where(item => item.StudentId == userId && item.IsMeasured)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.RawWpm, item.ComprehensionScore })
            .ToListAsync(cancellationToken);
        var wpmResults = results.Where(item => item.RawWpm > 0).ToList();

        return new DailyProgressSummary(
            program.Value.Progress.Id,
            program.Value.Progress.CurrentDay,
            program.Value.Progress.DaysCompleted,
            program.Value.Progress.ExercisesCompleted,
            logs.Count,
            completed.Count,
            program.Value.Progress.AssignedDate,
            wpmResults.Count == 0 ? 0 : wpmResults.TakeLast(5).Average(item => item.RawWpm),
            wpmResults.Count == 0 ? 0 : wpmResults.Take(5).Average(item => item.RawWpm),
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
                && item.CompletedDate >= weekStart
                && item.CompletedDate < weekStart.AddDays(7))
            .ToListAsync(cancellationToken);

        return new WeeklyProgressSummary(
            logs.Count,
            logs.Count(item => item.IsPassed),
            logs.Where(item => item.IsPassed && item.IsMeasured)
                .Select(item => item.SuccessRate)
                .DefaultIfEmpty()
                .Average(),
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
            throw new ArgumentOutOfRangeException(nameof(month));
        if (targetYear is < 1 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(year));

        var firstDay = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = firstDay.AddMonths(1);
        var logs = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId
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
                group.Where(item => item.IsPassed && item.IsMeasured)
                    .Select(item => item.SuccessRate)
                    .DefaultIfEmpty()
                    .Average()))
            .ToList();

        return new DailyProgressCalendar(targetMonth, targetYear, days);
    }

    private async Task<(StudentProgramProgress Progress, ProgramTemplate Template)?> GetActiveProgramAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var progress = await db.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive && item.CompletedDate == null)
            .OrderByDescending(item => item.AssignedDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (progress is null)
            return null;

        var template = await db.ProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == progress.ProgramTemplateId && !item.IsDeleted, cancellationToken);
        return template is null ? null : (progress, template);
    }

    private async Task<(StudentProgramProgress Progress, ProgramTemplate Template)?> GetActiveOrLatestProgramAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var progress = await db.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.AssignedDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (progress is null)
            return null;

        var template = await db.ProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == progress.ProgramTemplateId && !item.IsDeleted, cancellationToken);
        return template is null ? null : (progress, template);
    }

    private async Task<List<DailyExerciseSummary>> BuildExercisesAsync(
        Guid userId,
        StudentProgramProgress progress,
        ProgramTemplate template,
        int week,
        int day,
        CancellationToken cancellationToken)
    {
        var patterns = GetPatterns(template.WeeklyPatternJson, week, day);
        if (patterns.Count == 0)
            return [];

        var completedLogs = await db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && item.StudentProgramProgressId == progress.Id
                && item.WeekNumber == week
                && item.DayNumber == day)
            .OrderBy(item => item.CompletedDate)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var completedByExercise = completedLogs
            .GroupBy(item => item.ExerciseId)
            .ToDictionary(group => group.Key, group => new Queue<DailyExerciseLog>(group));

        var result = new List<DailyExerciseSummary>();
        var order = 1;
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern.Type) || pattern.Count <= 0)
                continue;

            var exerciseType = await db.ExerciseTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Name == pattern.Type && item.IsActive, cancellationToken);
            if (exerciseType is null)
                continue;

            var candidates = await FindCandidatesAsync(
                exerciseType.Id,
                pattern.Difficulty,
                template,
                pattern.Count,
                cancellationToken);
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
        StudentProgramProgress progress,
        ProgramTemplate template,
        int week,
        int day,
        CancellationToken cancellationToken)
    {
        var patterns = GetPatterns(template.WeeklyPatternJson, week, day);
        var count = 0;
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern.Type) || pattern.Count <= 0)
                continue;

            var typeId = await db.ExerciseTypes
                .AsNoTracking()
                .Where(item => item.Name == pattern.Type && item.IsActive)
                .Select(item => (Guid?)item.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (!typeId.HasValue)
                continue;

            var candidates = await FindCandidatesAsync(
                typeId.Value,
                pattern.Difficulty,
                template,
                pattern.Count,
                cancellationToken);
            count += candidates.Count;
        }

        return count;
    }

    private async Task<List<Exercise>> FindCandidatesAsync(
        Guid exerciseTypeId,
        int difficulty,
        ProgramTemplate template,
        int requestedCount,
        CancellationToken cancellationToken)
    {
        var candidates = await db.Exercises
            .AsNoTracking()
            .Where(item => item.ExerciseTypeId == exerciseTypeId
                && item.DifficultyLevel == difficulty
                && (item.TargetAgeGroupId == null
                    || item.TargetAgeGroupId == template.TargetAgeGroupConfigurationId)
                && item.IsActive)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            for (var fallbackDifficulty = difficulty - 1;
                fallbackDifficulty >= 1 && candidates.Count == 0;
                fallbackDifficulty--)
            {
                candidates = await db.Exercises
                    .AsNoTracking()
                    .Where(item => item.ExerciseTypeId == exerciseTypeId
                        && item.DifficultyLevel == fallbackDifficulty
                        && (item.TargetAgeGroupId == null
                            || item.TargetAgeGroupId == template.TargetAgeGroupConfigurationId)
                        && item.IsActive)
                    .OrderBy(item => item.Id)
                    .ToListAsync(cancellationToken);
            }
        }

        if (candidates.Count == 0)
            return [];

        return Enumerable.Range(0, requestedCount)
            .Select(index => candidates[index % candidates.Count])
            .ToList();
    }

    private static List<ExercisePattern> GetPatterns(string json, int week, int day)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var daily = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<ExercisePattern>>>>(
                json,
                JsonOptions);
            if (daily is not null && daily.Count > 0)
            {
                var weekData = daily.GetValueOrDefault($"week{week}")
                    ?? daily.GetValueOrDefault("week1");
                if (weekData is null)
                    return [];

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
