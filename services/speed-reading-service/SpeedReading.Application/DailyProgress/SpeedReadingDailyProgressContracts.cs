using System.Text.Json;

namespace SpeedReading.Application.DailyProgress;

public sealed record DailyExerciseSummary(
    Guid ExerciseId,
    Guid ExerciseTypeId,
    string ExerciseTypeName,
    string Title,
    string Description,
    int DifficultyLevel,
    int EstimatedDurationMinutes,
    bool IsCompleted,
    DateTime? CompletedAt,
    int Order,
    string ConfigurationJson,
    bool IsBonus = false);

public sealed class CompleteDailyExerciseRequest
{
    // Source API names.
    public Guid? ExerciseLogId { get; init; }
    public decimal? Score { get; init; }
    public int? DurationSeconds { get; init; }

    // The current frontend sends these names. Both contracts are supported
    // during the migration so old clients do not silently lose completions.
    public Guid? ExerciseId { get; init; }
    public Guid? SessionId { get; init; }
    public int? SlotOrder { get; init; }
    public Guid? ProgramProgressId { get; init; }
    public int? ProgramDay { get; init; }
    public decimal? SuccessRate { get; init; }
    public int? TimeSpentSeconds { get; init; }
    public string? MeasurementStatus { get; init; }

    public int CorrectCount { get; init; }
    public int IncorrectCount { get; init; }
    public int TotalAttempts { get; init; }
    public decimal AverageResponseTimeMs { get; init; }
    public decimal MedianResponseTimeMs { get; init; }
    public decimal StdDevResponseTimeMs { get; init; }
    public int PauseCount { get; init; }
    public int TotalPausedSeconds { get; init; }
    public string? DevicePlatform { get; init; }
    public string? ResultDataJson { get; init; }
}

public sealed record RecommendedProgramSummary(
    Guid TemplateId,
    string Name,
    string Description,
    int TotalDays,
    int TotalWeeks,
    string DifficultyRange,
    string RecommendationReason,
    string ProgramType);

public sealed record ProgramCompletionStats(
    int TotalDays,
    decimal AverageSuccessRate,
    int LongestStreak,
    int TotalExercises);

public sealed record CompleteDailyExerciseResponse(
    bool Success,
    string Message,
    bool DayCompleted,
    int CurrentDay,
    int CurrentWeek,
    int CurrentDifficultyLevel,
    bool DifficultyIncreased,
    int OldDifficultyLevel,
    bool WeekChanged,
    int OldWeek,
    int? NewDifficultyLevel,
    int CurrentStreak,
    int LongestStreak,
    bool ProgramCompleted,
    RecommendedProgramSummary? RecommendedNextProgram,
    ProgramCompletionStats? CompletionStats,
    bool AdaptationUpdated = false);

public sealed record StudentProgressSummary(
    int CurrentWeek,
    int CurrentDay,
    int CurrentDifficultyLevel,
    int ExercisesCompleted,
    decimal AverageSuccessRate,
    int CurrentStreak,
    int LongestStreak,
    DateTime? LastCompletionDate,
    string TemplateName,
    int TotalWeeks,
    int TotalDays,
    int TotalExercisesCompleted);

public sealed record DailyProgressSummary(
    Guid Id,
    int CurrentDay,
    int DaysCompleted,
    int ExercisesCompleted,
    int TotalExercises,
    int CompletedExercises,
    DateTime AssignedDate,
    decimal CurrentReadingSpeed,
    decimal InitialReadingSpeed,
    decimal AverageSuccessRate,
    int CurrentWeek);

public sealed record WeeklyProgressSummary(
    int TotalExercises,
    int CompletedExercises,
    decimal AverageScore,
    int TotalMinutes,
    int ActiveDays);

public sealed record DailyProgressCalendarDay(
    DateTime Date,
    int TotalExercises,
    int CompletedExercises,
    decimal AverageScore);

public sealed record DailyProgressCalendar(
    int Month,
    int Year,
    IReadOnlyList<DailyProgressCalendarDay> Days);

public interface ISpeedReadingDailyProgress
{
    Task<IReadOnlyList<DailyExerciseSummary>> GetTodayExercisesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyExerciseSummary>> GetExercisesByDayAsync(
        Guid userId,
        int dayNumber,
        CancellationToken cancellationToken = default);

    Task<CompleteDailyExerciseResponse> CompleteExerciseAsync(
        Guid userId,
        CompleteDailyExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<DailyProgressSummary?> GetProgressSummaryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<WeeklyProgressSummary> GetWeeklyStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DailyProgressCalendar> GetCalendarAsync(
        Guid userId,
        int? month,
        int? year,
        CancellationToken cancellationToken = default);
}

public static class SpeedReadingDailyProgressRules
{
    public static int GetAvailableDay(DateTime assignedAt, DateTime now, bool isStaffTraining, int earlyAdvanceDays = 0) =>
        isStaffTraining ? int.MaxValue : checked(GetCalendarAvailableDay(assignedAt, now) + Math.Max(0, earlyAdvanceDays));
    public static int GetCalendarAvailableDay(DateTime assignedAt, DateTime now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(assignedAt, zone));
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone));
        return Math.Max(1, today.DayNumber - start.DayNumber + 1);
    }

    public static int CountCompletedScheduledDays(
        IEnumerable<(int Week, int Day, Guid ExerciseId)> scheduled,
        IEnumerable<(int Week, int Day, Guid ExerciseId)> completed,
        int maximumDay)
    {
        var completedSlots = completed.ToHashSet();
        return scheduled
            .Where(slot => ((slot.Week - 1) * 7) + slot.Day <= maximumDay)
            .GroupBy(slot => (slot.Week, slot.Day))
            .Count(day => day.All(slot => completedSlots.Contains(slot)));
    }

    public static string ValidateIdempotencyKey(string? key)
    {
        var normalized = key?.Trim() ?? string.Empty;
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                "^[A-Za-z0-9._~-]{16,128}$"))
        {
            throw new ArgumentException(
                "Idempotency-Key must contain 16-128 letters, numbers, dots, underscores, hyphens or tildes.",
                nameof(key));
        }

        return normalized;
    }

    public static decimal ResolveScore(decimal? legacyScore, decimal? frontendSuccessRate)
    {
        var score = legacyScore ?? frontendSuccessRate
            ?? throw new ArgumentException("Score or SuccessRate is required.");

        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(legacyScore), "Score must be between 0 and 100.");
        }

        return score;
    }

    public static int ResolveDuration(int? legacyDuration, int? frontendDuration)
    {
        var duration = legacyDuration ?? frontendDuration
            ?? throw new ArgumentException("DurationSeconds or TimeSpentSeconds is required.");
        return ValidateDuration(duration);
    }

    public static int ValidateDuration(int duration)
    {
        if (duration <= 0 || duration > 24 * 60 * 60)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be between 1 second and 24 hours.");
        }

        return duration;
    }

    // Configured display/working time only; answering and breaks are excluded.
    public static int EstimateDurationMinutes(string configurationJson)
    {
        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("engineType", out var engine) || engine.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("engineConfig", out var settings) || settings.ValueKind != JsonValueKind.Object)
                return 0;
            static double Number(JsonElement obj, string name) => obj.ValueKind == JsonValueKind.Object
                && obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out var n) && double.IsFinite(n) && n > 0 ? n : 0;
            static JsonElement Object(JsonElement obj, string name) => obj.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.Object ? value : default;
            var timing = Object(settings, "timing");
            var content = Object(settings, "content");
            var seconds = Number(timing, "durationSeconds");
            if (engine.GetString() == "focus")
                seconds = Number(settings, "speedMs") * Number(settings, "totalSteps") / 1000;
            else if (engine.GetString() == "free_reading")
                seconds = Number(timing, "suggestedMaxReadingTimeMs") / 1000;
            else if (engine.GetString() is "word_highlight" or "text_fade" or "regression_reduction" or "subvocalization_reduction")
            {
                var pace = Number(Object(settings, "pacer"), "speedWpm");
                if (pace == 0) pace = Number(Object(settings, "fading"), "speedWpm");
                if (pace == 0) pace = Number(settings, "targetWpm");
                seconds = pace > 0 ? Number(content, "maxWordCount") * 60 / pace : 0;
                if (seconds > 0 && engine.GetString() == "text_fade") seconds += Number(Object(settings, "fading"), "lagMs") / 1000;
            }
            else if (engine.GetString() == "text_stream" && settings.TryGetProperty("mode", out var mode)
                && mode.ValueKind == JsonValueKind.String && mode.GetString() == "rsvp")
                seconds = Number(timing, "durationMs") * Number(content, "maxWordCount") / 1000;
            return seconds is > 0 and <= 86400 ? (int)Math.Ceiling(seconds / 60) : 0;
        }
        catch (JsonException) { return 0; }
    }

    // Legacy Focus-only API retained for older consumers.
    public static int EstimateFocusDurationMinutes(string configurationJson)
    {
        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("engineType", out var engine) || engine.ValueKind != JsonValueKind.String || engine.GetString() != "focus"
                || !root.TryGetProperty("engineConfig", out var settings)
                || settings.ValueKind != JsonValueKind.Object
                || !settings.TryGetProperty("speedMs", out var speed) || speed.ValueKind != JsonValueKind.Number || !speed.TryGetInt32(out var speedMs)
                || !settings.TryGetProperty("totalSteps", out var count) || count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out var steps)
                || speedMs is < 100 or > 10000 || steps is < 2 or > 500)
                return 5;
            return (int)Math.Ceiling(speedMs * (double)steps / 60000);
        }
        catch (JsonException)
        {
            // Imported legacy catalogue JSON is also tolerated by the existing
            // configuration sanitizer. Duration display must not break the list.
            return 5;
        }
    }

    public static int CalculateNextStreak(
        DateTime? lastCompletionDate,
        DateTime completionDate,
        int currentStreak) =>
        SpeedReading.Domain.Gamification.GamificationRules.CalculateNextStreak(lastCompletionDate, completionDate, currentStreak);

    public const decimal PassingScore = 70m;

    /// <summary>
    /// Review H5 (finding 16): the one "passed" rule. Unmeasured or free-reading work is done (not failed); measured
    /// work passes at <see cref="PassingScore"/>.
    /// </summary>
    public static bool IsPassed(bool isMeasured, decimal score, bool isFreeReadingPractice) =>
        !isMeasured || isFreeReadingPractice || score >= PassingScore;

    public static (int Week, int Day) GetWeekAndDay(int cumulativeDay)
    {
        if (cumulativeDay < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cumulativeDay));
        }

        return (((cumulativeDay - 1) / 7) + 1, ((cumulativeDay - 1) % 7) + 1);
    }

    /// <summary>
    /// Review H5 (finding 19, decision 29.10.2026): a finished program week moves the next weeks' difficulty by its
    /// measured average — 80 or more up one, under 60 down one, otherwise unchanged (no measurement: unchanged).
    /// </summary>
    public static int WeeklyDifficultyStep(decimal? weekAverage) => weekAverage switch
    {
        null => 0,
        >= 80 => 1,
        < 60 => -1,
        _ => 0
    };

    public static int ResolveExerciseDifficulty(
        int configuredDifficulty,
        int currentDifficulty,
        int initialDifficulty,
        int maximumDifficulty)
    {
        if (configuredDifficulty < 0 || initialDifficulty < 0 || maximumDifficulty < initialDifficulty)
            throw new ArgumentOutOfRangeException(nameof(configuredDifficulty));

        return Math.Clamp(
            configuredDifficulty + (currentDifficulty - initialDifficulty),
            0,
            maximumDifficulty);
    }

    public static string? GetDailyPatternJson(string programJson, int week, int day)
    {
        if (string.IsNullOrWhiteSpace(programJson) || week < 1 || day < 1)
            return null;

        try
        {
            using var document = JsonDocument.Parse(programJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!document.RootElement.TryGetProperty($"week{week}", out var weekPattern)
                && !document.RootElement.TryGetProperty("week1", out weekPattern))
            {
                return null;
            }

            if (weekPattern.ValueKind == JsonValueKind.Array)
                return weekPattern.GetRawText();

            if (weekPattern.ValueKind != JsonValueKind.Object)
                return null;

            if (!weekPattern.TryGetProperty($"day{day}", out var dayPattern)
                && !weekPattern.TryGetProperty("day1", out dayPattern))
            {
                return null;
            }

            return dayPattern.ValueKind == JsonValueKind.Array
                ? dayPattern.GetRawText()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<T> TakeUnique<T>(IReadOnlyList<T> candidates, int requestedCount)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (requestedCount <= 0 || candidates.Count == 0)
            return [];

        return candidates.Take(requestedCount).ToList();
    }
}
