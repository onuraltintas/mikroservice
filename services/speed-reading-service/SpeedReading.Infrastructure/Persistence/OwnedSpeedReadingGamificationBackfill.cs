using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Gamification;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingGamificationBackfillResult(
    int AchievementsInserted,
    int UserGamificationRowsInserted,
    int UserAchievementsInserted,
    int ExistingRows,
    DateTime CompletedAtUtc);

public sealed class OwnedSpeedReadingGamificationBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedSpeedReadingGamificationBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await owned.Database.MigrateAsync(cancellationToken);
        var achievements = await legacy.Achievements
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var userGamification = await legacy.UserGamifications
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var userAchievements = await legacy.UserAchievements
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var achievementIds = achievements.Select(item => item.Id).ToHashSet();
        var userGamificationIds = userGamification.Select(item => item.Id).ToHashSet();
        ValidateReferences(achievements, userGamification, userAchievements, achievementIds);

        await using var transaction = await owned.Database.BeginTransactionAsync(cancellationToken);
        var existingRows = 0;
        var achievementCount = 0;
        var gamificationCount = 0;
        var userAchievementCount = 0;

        var existingAchievementIds = await owned.Achievements
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in achievements)
        {
            if (existingAchievementIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.Achievements.Add(Achievement.Import(
                source.Id,
                source.Name,
                source.Description,
                source.Category,
                source.Tier,
                source.IconUrl,
                source.IconEmoji,
                source.CriteriaType,
                source.CriteriaValue,
                source.TriggerType,
                source.TriggerValue,
                source.IsRepeatable,
                source.XPReward,
                source.IsActive,
                source.SortOrder,
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            achievementCount++;
        }

        var existingGamificationIds = await owned.UserGamifications
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in userGamification)
        {
            if (existingGamificationIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.UserGamifications.Add(UserGamification.Import(
                source.Id,
                source.UserId,
                source.TotalXP,
                source.CurrentLevel,
                source.CurrentLevelXP,
                source.NextLevelXP,
                source.LevelTitle,
                source.LevelIcon,
                source.CurrentStreak,
                source.LongestStreak,
                NormalizeUtc(source.LastActivityDate),
                source.StreakFreezeCount,
                source.TotalActivitiesCompleted,
                source.TotalReadingMinutes,
                source.MaxWPM,
                source.MaxComprehensionScore,
                source.TotalExercisesCompleted,
                source.TotalReadingSessionsCompleted,
                source.CompletedExerciseTypesJson,
                source.MaxRSVPWPM,
                source.MaxRSVPComprehension,
                source.TotalVocabularyWordsLearned,
                source.MaxVocabularyBoxReached,
                source.TotalVocabularyQuestionsAnswered,
                source.VocabularyMasteryLevel,
                source.MaxVocabularyStreak,
                source.LearnedVocabularyCategoriesJson,
                source.LearnedVocabularyCategoriesMapJson,
                source.LearnedVocabularyDifficultiesJson,
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            gamificationCount++;
        }

        var existingUserAchievementIds = await owned.UserAchievements
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in userAchievements)
        {
            if (existingUserAchievementIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.UserAchievements.Add(UserAchievement.Import(
                source.Id,
                source.UserId,
                source.AchievementId,
                NormalizeUtc(source.UnlockedAt),
                source.IsShowcased,
                source.ShowcaseOrder,
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            userAchievementCount++;
        }

        await owned.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OwnedSpeedReadingGamificationBackfillResult(
            achievementCount,
            gamificationCount,
            userAchievementCount,
            existingRows,
            DateTime.UtcNow);
    }

    private static void ValidateReferences(
        IReadOnlyList<LegacyAchievement> achievements,
        IReadOnlyList<LegacyUserGamification> userGamification,
        IReadOnlyList<LegacyUserAchievement> userAchievements,
        IReadOnlySet<Guid> achievementIds)
    {
        foreach (var achievement in achievements)
        {
            if (achievement.Id == Guid.Empty || string.IsNullOrWhiteSpace(achievement.Name))
                throw new InvalidOperationException($"Achievement {achievement.Id} is missing required data.");
        }

        foreach (var stats in userGamification)
        {
            if (stats.Id == Guid.Empty || stats.UserId == Guid.Empty)
                throw new InvalidOperationException($"User gamification row {stats.Id} has no valid user.");
        }

        foreach (var userAchievement in userAchievements)
        {
            if (userAchievement.UserId == Guid.Empty || !achievementIds.Contains(userAchievement.AchievementId))
                throw new InvalidOperationException(
                    $"User achievement {userAchievement.Id} references a missing user or achievement.");
        }
    }

    private static string? ToAuditValue(Guid? value) => value?.ToString();

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue ? NormalizeUtc(value.Value) : null;
}
