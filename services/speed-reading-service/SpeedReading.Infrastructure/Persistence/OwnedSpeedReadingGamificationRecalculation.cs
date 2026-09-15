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
        var rows = await (
            from result in db.ExerciseSessionResults.AsNoTracking()
            join exercise in db.Exercises.AsNoTracking() on result.ExerciseId equals exercise.Id
            join exerciseType in db.ExerciseTypes.AsNoTracking() on exercise.ExerciseTypeId equals exerciseType.Id
            where !result.IsAssessmentMode && result.AssessmentAttemptId == null
            select new SessionHistoryRow(
                result.StudentId,
                exerciseType.Name,
                result.CompletedAt,
                result.TimeSpentSeconds,
                result.RawWpm,
                result.ComprehensionScore,
                result.Score,
                result.IsMeasured,
                result.QuestionAnswersJson))
            .ToListAsync(cancellationToken);

        var historyByUser = rows
            .GroupBy(row => row.StudentId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(ToCompletion).ToList());
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

    private static VerifiedExerciseCompletion ToCompletion(SessionHistoryRow row)
    {
        var rawWpm = row.IsMeasured && row.RawWpm > 0
            ? (int?)Math.Round(row.RawWpm, MidpointRounding.AwayFromZero)
            : null;
        var accuracy = CalculateAccuracy(row.QuestionAnswersJson, row.ComprehensionScore);
        return new VerifiedExerciseCompletion(
            row.ExerciseType,
            row.CompletedAt,
            Math.Max(row.TimeSpentSeconds, 0),
            rawWpm,
            row.IsMeasured ? Math.Clamp(row.ComprehensionScore, 0, 100) : null,
            rawWpm.HasValue,
            row.IsMeasured
                ? SpeedReadingExerciseSessionRules.CalculateXp(
                    Math.Clamp(row.Score, 0, 100),
                    accuracy,
                    Math.Max(row.TimeSpentSeconds, 0))
                : 0);
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
        string ExerciseType,
        DateTime CompletedAt,
        int TimeSpentSeconds,
        decimal RawWpm,
        decimal ComprehensionScore,
        decimal Score,
        bool IsMeasured,
        string QuestionAnswersJson);

    private sealed class StoredAnswer
    {
        public bool IsCorrect { get; init; }
    }
}
