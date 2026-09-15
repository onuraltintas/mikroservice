using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Gamification;

namespace SpeedReading.Infrastructure.Persistence;

internal static class OwnedGamificationAchievementEvaluator
{
    public static async Task<IReadOnlyList<Achievement>> UnlockEligibleAsync(
        OwnedSpeedReadingDbContext db,
        UserGamification stats,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var unlockedIds = await db.UserAchievements
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .Select(item => item.AchievementId)
            .ToListAsync(cancellationToken);
        var achievements = await db.Achievements
            .Where(item => item.IsActive && !item.IsDeleted && !unlockedIds.Contains(item.Id))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var unlocked = new List<Achievement>();
        foreach (var achievement in achievements)
        {
            if (!MeetsCriteria(achievement, stats))
                continue;

            db.UserAchievements.Add(UserAchievement.Import(
                Guid.NewGuid(),
                userId,
                achievement.Id,
                now,
                false,
                null,
                false,
                null,
                null,
                now,
                userId.ToString(),
                null,
                null));
            stats.AwardXp(achievement.XPReward, userId, now);
            unlocked.Add(achievement);
        }

        return unlocked;
    }

    public static bool MeetsCriteria(Achievement achievement, UserGamification stats)
    {
        try
        {
            using var document = JsonDocument.Parse(achievement.CriteriaValue);
            var criteria = document.RootElement;
            return achievement.CriteriaType.Trim().ToLowerInvariant() switch
            {
                "streak" => MeetsInt(criteria, "days", stats.CurrentStreak),
                "level_reached" => MeetsInt(criteria, "level", stats.CurrentLevel),
                "activity_count" => MeetsInt(criteria, "count", stats.TotalActivitiesCompleted),
                "total_xp" => MeetsLong(criteria, "xp", stats.TotalXP),
                "reading_minutes" => MeetsInt(criteria, "minutes", stats.TotalReadingMinutes),
                "wpm_reached" => MeetsInt(criteria, "wpm", stats.MaxWPM),
                "comprehension_score" => MeetsDecimal(criteria, "score", stats.MaxComprehensionScore),
                "reading_count" => MeetsInt(criteria, "count", stats.TotalReadingSessionsCompleted),
                "rsvp_count" => MeetsInt(criteria, "count", stats.TotalRsvpSessionsCompleted),
                "exercise_count" => MeetsInt(criteria, "count", stats.TotalExercisesCompleted),
                "vocabulary_learned" => MeetsInt(criteria, "count", stats.TotalVocabularyWordsLearned),
                "vocabulary_box" => MeetsInt(criteria, "box", stats.MaxVocabularyBoxReached),
                "vocabulary_streak" => MeetsInt(criteria, "count", stats.MaxVocabularyStreak),
                "vocabulary_categories" => MeetsInt(criteria, "count", DeserializeCount(stats.LearnedVocabularyCategoriesJson)),
                "rsvp_wpm" => MeetsInt(criteria, "wpm", stats.MaxRSVPWPM),
                "rsvp_comprehension" => MeetsDecimal(criteria, "score", stats.MaxRSVPComprehension),
                "exercise_type_first" => MeetsType(criteria, stats.CompletedExerciseTypesJson),
                "exercise_variety" => MeetsInt(criteria, "types", DeserializeCount(stats.CompletedExerciseTypesJson)),
                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int DeserializeCount(string json) =>
        JsonSerializer.Deserialize<List<string>>(json)?.Count ?? 0;

    private static bool MeetsInt(JsonElement criteria, string name, int current) =>
        criteria.TryGetProperty(name, out var value) && value.TryGetInt32(out var target) && current >= target;

    private static bool MeetsLong(JsonElement criteria, string name, long current) =>
        criteria.TryGetProperty(name, out var value) && value.TryGetInt64(out var target) && current >= target;

    private static bool MeetsDecimal(JsonElement criteria, string name, decimal current) =>
        criteria.TryGetProperty(name, out var value) && value.TryGetDecimal(out var target) && current >= target;

    private static bool MeetsType(JsonElement criteria, string jsonTypes)
    {
        if (!criteria.TryGetProperty("type", out var value) || value.ValueKind != JsonValueKind.String)
            return false;
        return JsonSerializer.Deserialize<List<string>>(jsonTypes)?.Contains(
            value.GetString() ?? string.Empty,
            StringComparer.OrdinalIgnoreCase) == true;
    }
}
