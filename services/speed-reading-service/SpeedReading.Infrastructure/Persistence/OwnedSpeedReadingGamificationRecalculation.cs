using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Gamification;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingGamificationRecalculationResult(
    int UsersRebuilt,
    int SessionsProcessed,
    int AchievementsUnlocked,
    DateTime CompletedAtUtc);

/// <summary>
/// Rebuilds only exercise-derived statistics from immutable, verified session
/// results. Vocabulary progress and existing badge ownership are retained.
/// H19 (audit 30.10.2026): the rebuild applies the live rules. A result counts when it was recorded as counting; a
/// legacy result without that flag counts only if it was measured, the reading was complete and a vocabulary session
/// was a quiz. XP is paid for the first counted completion of an exercise per Istanbul day only.
/// </summary>
public sealed class OwnedSpeedReadingGamificationRecalculation(
    OwnedSpeedReadingDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<OwnedSpeedReadingGamificationRecalculationResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var candidates = await (
            from result in db.ExerciseSessionResults.AsNoTracking()
            join exercise in db.Exercises.AsNoTracking() on result.ExerciseId equals exercise.Id
            join exerciseType in db.ExerciseTypes.AsNoTracking() on exercise.ExerciseTypeId equals exerciseType.Id
            join session in db.ExerciseSessions.AsNoTracking() on result.SessionId equals session.Id into sessions
            from session in sessions.DefaultIfEmpty()
            where !result.IsAssessmentMode && result.AssessmentAttemptId == null
                && (result.CountsForGamification == true || (result.CountsForGamification == null && result.IsMeasured))
            select new SessionHistoryRow(
                result.StudentId,
                result.ExerciseId,
                exerciseType.Name,
                exerciseType.EngineType,
                result.CompletedAt,
                result.TimeSpentSeconds,
                result.RawWpm,
                result.ComprehensionScore,
                result.Score,
                result.IsMeasured,
                result.QuestionAnswersJson,
                result.GamificationXp,
                result.CountsForGamification,
                session == null ? null : session.SessionDataJson))
            .ToListAsync(cancellationToken);
        var rows = candidates
            .Select(row => (Row: row, State: ReadState(row.SessionDataJson)))
            .Where(item => item.Row.CountsForGamification == true || CountsLegacy(item.State))
            .OrderBy(item => item.Row.CompletedAt)
            .ToList();

        var paidToday = new HashSet<(Guid StudentId, Guid ExerciseId, DateTime Day)>();
        var historyByUser = rows
            .Select(item => (item.Row, Completion: ToCompletion(item.Row, item.State,
                paidToday.Add((item.Row.StudentId, item.Row.ExerciseId, GamificationRules.IstanbulDate(item.Row.CompletedAt))))))
            .GroupBy(item => item.Row.StudentId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Completion).ToList());
        if (historyByUser.Count == 0)
        {
            return new OwnedSpeedReadingGamificationRecalculationResult(
                0,
                0,
                0,
                DateTime.UtcNow);
        }

        var userIds = historyByUser.Keys.ToList();
        var statsByUser = await db.UserGamifications
            .Where(item => userIds.Contains(item.UserId) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.UserId, cancellationToken);
        var achievementXpByUser = await (
            from userAchievement in db.UserAchievements.AsNoTracking()
            join achievement in db.Achievements.AsNoTracking()
                on userAchievement.AchievementId equals achievement.Id
            where userIds.Contains(userAchievement.UserId)
                && !userAchievement.IsDeleted
                && !achievement.IsDeleted
            select new { userAchievement.UserId, achievement.XPReward })
            .ToListAsync(cancellationToken);
        var rewardByUser = achievementXpByUser
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => checked(group.Sum(item => item.XPReward)));

        var now = DateTime.UtcNow;
        var achievementsUnlocked = 0;
        foreach (var (userId, completions) in historyByUser)
        {
            if (!statsByUser.TryGetValue(userId, out var stats))
            {
                stats = UserGamification.CreateDefault(Guid.NewGuid(), userId, now, userId.ToString());
                db.UserGamifications.Add(stats);
            }

            stats.RebuildVerifiedExerciseHistory(
                completions,
                rewardByUser.GetValueOrDefault(userId),
                userId,
                now);
            var unlocked = await OwnedGamificationAchievementEvaluator.UnlockEligibleAsync(
                db,
                stats,
                userId,
                now,
                cancellationToken);
            achievementsUnlocked += unlocked.Count;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new OwnedSpeedReadingGamificationRecalculationResult(
            historyByUser.Count,
            rows.Count,
            achievementsUnlocked,
            now);
    }

    private static VerifiedExerciseCompletion ToCompletion(SessionHistoryRow row, StoredState state, bool firstToday)
    {
        var rawWpm = row.IsMeasured && row.RawWpm > 0
            ? (int?)Math.Round(row.RawWpm, MidpointRounding.AwayFromZero)
            : null;
        var accuracy = CalculateAccuracy(row.QuestionAnswersJson, row.ComprehensionScore);
        var questionCount = CountAnswers(row.QuestionAnswersJson);
        var xp = row.GamificationXp ?? (row.IsMeasured
            ? SpeedReadingExerciseSessionRules.CalculateXp(
                Math.Clamp(row.Score, 0, 100),
                accuracy,
                Math.Max(row.TimeSpentSeconds, 0))
            : 0);
        var isRsvp = row.ExerciseType.Trim().Equals("RSVP", StringComparison.OrdinalIgnoreCase);
        return new VerifiedExerciseCompletion(
            row.ExerciseType,
            row.CompletedAt,
            Math.Max(row.TimeSpentSeconds, 0),
            rawWpm,
            row.IsMeasured && questionCount > 0 ? Math.Clamp(row.ComprehensionScore, 0, 100) : null,
            rawWpm.HasValue,
            firstToday ? xp : 0,
            CountsForComprehensionBadge: row.IsMeasured && questionCount >= 3
                && string.Equals(row.EngineType, "reading_comprehension", StringComparison.OrdinalIgnoreCase)
                && state.ComprehensionMode != "learning",
            RsvpPaceWpm: isRsvp && !state.ReadingIncomplete && state.RsvpDisplayPaceWpm > 0
                && state.RsvpCompletionPercent >= 100
                    ? (int)Math.Round(state.RsvpDisplayPaceWpm, MidpointRounding.AwayFromZero)
                    : null);
    }

    /// <summary>Live rule for results recorded before the counting flag existed.</summary>
    private static bool CountsLegacy(StoredState state) =>
        !state.ReadingIncomplete && (state.VocabularyWords.Count == 0 || state.VocabularyMode == "quiz");

    private static StoredState ReadState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new StoredState();
        try
        {
            return JsonSerializer.Deserialize<StoredState>(json, JsonOptions) ?? new StoredState();
        }
        catch (JsonException)
        {
            return new StoredState();
        }
    }

    private static int CountAnswers(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<StoredAnswer>>(json, JsonOptions)?.Count ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static decimal CalculateAccuracy(string json, decimal fallback)
    {
        try
        {
            var answers = JsonSerializer.Deserialize<List<StoredAnswer>>(json, JsonOptions) ?? [];
            if (answers.Count == 0)
                return Math.Clamp(fallback, 0, 100);
            return SpeedReadingExerciseSessionRules.CalculateAccuracy(
                answers.Count(item => item.IsCorrect),
                answers.Count(item => !item.IsCorrect));
        }
        catch (JsonException)
        {
            return Math.Clamp(fallback, 0, 100);
        }
    }

    private sealed record SessionHistoryRow(
        Guid StudentId,
        Guid ExerciseId,
        string ExerciseType,
        string EngineType,
        DateTime CompletedAt,
        int TimeSpentSeconds,
        decimal RawWpm,
        decimal ComprehensionScore,
        decimal Score,
        bool IsMeasured,
        string QuestionAnswersJson,
        int? GamificationXp,
        bool? CountsForGamification,
        string? SessionDataJson);

    private sealed class StoredState
    {
        public bool ReadingIncomplete { get; init; }
        public List<JsonElement> VocabularyWords { get; init; } = [];
        public string? VocabularyMode { get; init; }
        public string? ComprehensionMode { get; init; }
        public decimal RsvpDisplayPaceWpm { get; init; }
        public decimal RsvpCompletionPercent { get; init; }
    }

    private sealed class StoredAnswer
    {
        public bool IsCorrect { get; init; }
    }
}
