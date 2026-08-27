using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using SpeedReading.Application.Gamification;
using SpeedReading.Domain.Gamification;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingGamification(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingUserDirectory userDirectory) : ILegacySpeedReadingGamification
{
    public async Task<GamificationLevelUpResult> AwardXpAsync(
        Guid userId,
        AwardXpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid authenticated user is required.", nameof(userId));
        if (request.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.Amount), "XP amount must be greater than 0.");
        if (string.IsNullOrWhiteSpace(request.Source))
            throw new ArgumentException("Source is required.", nameof(request.Source));

        var stats = await GetOrCreateStatsAsync(userId, cancellationToken);
        var oldLevel = Math.Max(stats.CurrentLevel, 1);
        var oldTier = SpeedReadingGamificationRules.GetTier(oldLevel);
        stats.AwardXp(request.Amount, userId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        var newTier = SpeedReadingGamificationRules.GetTier(stats.CurrentLevel);
        return new GamificationLevelUpResult(
            stats.CurrentLevel > oldLevel,
            oldLevel,
            stats.CurrentLevel,
            oldTier,
            newTier,
            newTier != oldTier,
            stats.LevelTitle,
            stats.LevelIcon,
            [],
            stats.TotalXP,
            stats.CurrentLevelXP,
            stats.NextLevelXP);
    }

    public async Task<IReadOnlyList<AchievementSummary>> CheckAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (stats is null)
            return [];

        var unlockedIds = await db.UserAchievements
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .Select(item => item.AchievementId)
            .ToListAsync(cancellationToken);
        var achievements = await db.Achievements
            .Where(item => item.IsActive && !item.IsDeleted && !unlockedIds.Contains(item.Id))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var unlocked = new List<AchievementSummary>();
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
            unlocked.Add(ToSummary(achievement));
        }

        if (unlocked.Count == 0)
            return [];
        stats.RecalculateLevel();
        stats.MarkUpdated(userId, now);
        await db.SaveChangesAsync(cancellationToken);
        return unlocked;
    }

    public async Task<bool> UpdateShowcaseAsync(
        Guid userId,
        IReadOnlyList<Guid> achievementIds,
        CancellationToken cancellationToken = default)
    {
        if (achievementIds.Count > 5)
            throw new InvalidOperationException("Cannot showcase more than 5 achievements.");
        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User gamification stats not found.");
        var requestedIds = achievementIds.Distinct().ToArray();
        var unlocked = await db.UserAchievements
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (requestedIds.Any(id => unlocked.All(item => item.AchievementId != id)))
            throw new InvalidOperationException("Only unlocked achievements can be showcased.");

        var now = DateTime.UtcNow;
        foreach (var item in unlocked)
        {
            var index = Array.IndexOf(requestedIds, item.AchievementId);
            item.SetShowcase(index >= 0, index >= 0 ? index + 1 : null, userId, now);
        }
        stats.MarkUpdated(userId, now);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateStreakAsync(
        Guid userId,
        UpdateGamificationStreakRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DurationMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(request.DurationMinutes));
        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User gamification stats not found.");
        var activityDate = request.ActivityDate == default ? DateTime.UtcNow : request.ActivityDate;
        stats.UpdateStreak(activityDate, request.DurationMinutes, userId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UseStreakFreezeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User gamification stats not found.");
        stats.UseStreakFreeze(userId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<GamificationSummary> GetUserGamificationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var item = await db.UserGamifications.AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserId == userId && !value.IsDeleted, cancellationToken);
        return item is null
            ? new GamificationSummary(userId, 0, 1, 0, 100, "Başlangıç Okuyucu", "📖", 0, 0, null, 0, 0, 0, DateTime.UtcNow, null)
            : ToSummary(item);
    }

    public async Task<IReadOnlyList<AchievementSummary>> GetAchievementsAsync(
        CancellationToken cancellationToken = default) =>
        (await db.Achievements.AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken))
        .Select(ToSummary)
        .ToList();

    public async Task<IReadOnlyList<UserAchievementSummary>> GetUserAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from userAchievement in db.UserAchievements.AsNoTracking()
            join achievement in db.Achievements.AsNoTracking()
                on userAchievement.AchievementId equals achievement.Id
            where userAchievement.UserId == userId
                && !userAchievement.IsDeleted
                && !achievement.IsDeleted
            orderby userAchievement.UnlockedAt descending
            select new { userAchievement, achievement })
            .ToListAsync(cancellationToken);
        return rows.Select(item => new UserAchievementSummary(
            item.userAchievement.Id,
            item.userAchievement.UserId,
            item.userAchievement.AchievementId,
            item.userAchievement.UnlockedAt,
            item.userAchievement.IsShowcased,
            item.userAchievement.ShowcaseOrder,
            ToSummary(item.achievement))).ToList();
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        string type,
        int skip,
        int take,
        Guid requestingUserId,
        bool isGlobalAdministrator,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = type.Trim();
        if (!LeaderboardTypes.Contains(normalizedType, StringComparer.OrdinalIgnoreCase))
            throw new BusinessRuleException("SpeedReading.LeaderboardType.Invalid", "Geçersiz liderlik tablosu türü.");
        skip = Math.Clamp(skip, 0, 10_000);
        take = Math.Clamp(take, 1, 100);

        var query = db.UserGamifications.AsNoTracking().Where(item => !item.IsDeleted);
        if (!isGlobalAdministrator)
        {
            var institutionId = await db.UserProfiles.AsNoTracking()
                .Where(item => item.UserId == requestingUserId && item.IsActive)
                .Select(item => item.InstitutionId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!institutionId.HasValue)
                return [];
            query = query.Where(item => db.UserProfiles.Any(profile =>
                profile.UserId == item.UserId
                && profile.InstitutionId == institutionId
                && profile.IsActive));
        }

        query = normalizedType.ToLowerInvariant() switch
        {
            "totalxp" => query.OrderByDescending(item => item.TotalXP).ThenBy(item => item.UserId),
            "currentlevel" => query.OrderByDescending(item => item.CurrentLevel).ThenByDescending(item => item.TotalXP).ThenBy(item => item.UserId),
            "currentstreak" => query.OrderByDescending(item => item.CurrentStreak).ThenByDescending(item => item.TotalXP).ThenBy(item => item.UserId),
            "longeststreak" => query.OrderByDescending(item => item.LongestStreak).ThenByDescending(item => item.TotalXP).ThenBy(item => item.UserId),
            "totalactivities" => query.OrderByDescending(item => item.TotalActivitiesCompleted).ThenByDescending(item => item.TotalXP).ThenBy(item => item.UserId),
            "readingminutes" => query.OrderByDescending(item => item.TotalReadingMinutes).ThenByDescending(item => item.TotalXP).ThenBy(item => item.UserId),
            _ => query.OrderByDescending(item => item.TotalXP).ThenBy(item => item.UserId)
        };

        var rows = await query.Skip(skip).Take(take).Select(item => new LeaderboardRow(
            item.UserId,
            item.TotalXP,
            item.CurrentLevel,
            item.CurrentStreak,
            item.LongestStreak,
            item.TotalActivitiesCompleted,
            item.TotalReadingMinutes,
            item.LevelTitle,
            item.LevelIcon)).ToListAsync(cancellationToken);
        var userIds = rows.Select(item => item.UserId).ToArray();
        var directory = (await userDirectory.GetUsersAsync(userIds, cancellationToken)).Users
            .ToDictionary(item => item.UserId);
        var showcased = await (
            from userAchievement in db.UserAchievements.AsNoTracking()
            join achievement in db.Achievements.AsNoTracking()
                on userAchievement.AchievementId equals achievement.Id
            where userIds.Contains(userAchievement.UserId)
                && userAchievement.IsShowcased
                && !userAchievement.IsDeleted
                && !achievement.IsDeleted
            orderby userAchievement.ShowcaseOrder, achievement.SortOrder, achievement.Name
            select new { userAchievement.UserId, achievement })
            .ToListAsync(cancellationToken);
        var showcasedByUser = showcased.GroupBy(item => item.UserId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<AchievementSummary>)group.Select(item => ToSummary(item.achievement)).ToList());

        return rows.Select((item, index) => new LeaderboardEntry(
            item.UserId,
            directory.TryGetValue(item.UserId, out var user)
                ? DisplayName(user.FirstName, user.LastName, user.Email, item.UserId)
                : item.UserId.ToString("D"),
            skip + index + 1,
            GetValue(item, normalizedType),
            item.CurrentLevel,
            item.LevelTitle,
            item.LevelIcon,
            showcasedByUser.TryGetValue(item.UserId, out var achievements)
                ? achievements
                : Array.Empty<AchievementSummary>())).ToList();
    }

    public async Task<SpeedReadingPage<AchievementAdminSummary>> GetAchievementAdminPageAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        string? category,
        string? tier,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Clamp(pageNumber, 1, 1_000_000);
        var size = Math.Clamp(pageSize, 1, 100);
        var query = db.Achievements.AsNoTracking().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(item => item.Name.Contains(term) || item.Description.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(item => item.Category == category.Trim());
        if (!string.IsNullOrWhiteSpace(tier))
            query = query.Where(item => item.Tier == tier.Trim());
        if (isActive.HasValue)
            query = query.Where(item => item.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .Skip((page - 1) * size).Take(size)
            .Select(item => new
            {
                Achievement = item,
                UnlockedByUsersCount = db.UserAchievements.Count(userAchievement =>
                    userAchievement.AchievementId == item.Id && !userAchievement.IsDeleted)
            }).ToListAsync(cancellationToken);
        return new SpeedReadingPage<AchievementAdminSummary>(
            rows.Select(item => ToAdminSummary(item.Achievement, item.UnlockedByUsersCount)).ToList(),
            page,
            size,
            totalCount);
    }

    public async Task<AchievementAdminSummary> GetAchievementAdminAsync(
        Guid achievementId,
        CancellationToken cancellationToken = default)
    {
        var item = await db.Achievements.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == achievementId && !value.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Achievement", achievementId);
        var unlocked = await db.UserAchievements.CountAsync(
            value => value.AchievementId == achievementId && !value.IsDeleted,
            cancellationToken);
        return ToAdminSummary(item, unlocked);
    }

    public async Task<AchievementAdminStats> GetAchievementAdminStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Achievements.AsNoTracking().Where(item => !item.IsDeleted)
            .Select(item => new { item.Tier, item.Category, item.IsActive })
            .ToListAsync(cancellationToken);
        return new AchievementAdminStats(
            rows.Count,
            rows.Count(item => item.IsActive),
            rows.Count(item => !item.IsActive),
            rows.Count(item => item.Tier == "Bronze"),
            rows.Count(item => item.Tier == "Silver"),
            rows.Count(item => item.Tier == "Gold"),
            rows.Count(item => item.Tier == "Diamond"),
            rows.GroupBy(item => item.Category).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
    }

    private static readonly string[] LeaderboardTypes =
        ["TotalXP", "CurrentLevel", "CurrentStreak", "LongestStreak", "TotalActivities", "ReadingMinutes"];

    private async Task<UserGamification> GetOrCreateStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (stats is not null)
            return stats;
        stats = UserGamification.CreateDefault(Guid.NewGuid(), userId, DateTime.UtcNow, userId.ToString());
        db.UserGamifications.Add(stats);
        return stats;
    }

    private static bool MeetsCriteria(Achievement achievement, UserGamification stats)
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
                "reading_count" or "rsvp_count" => MeetsInt(criteria, "count", stats.TotalReadingSessionsCompleted),
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

    private static int DeserializeCount(string json) => JsonSerializer.Deserialize<List<string>>(json)?.Count ?? 0;

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

    private static string DisplayName(string firstName, string lastName, string? email, Guid id)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName)
            ? email ?? id.ToString("D")
            : fullName;
    }

    private static AchievementSummary ToSummary(Achievement item) => new(
        item.Id, item.Name, item.Description, item.Category, item.Tier, item.IconUrl, item.IconEmoji,
        item.CriteriaType, item.CriteriaValue, item.TriggerType, item.TriggerValue, item.IsRepeatable,
        item.XPReward, item.IsActive, item.SortOrder, item.CreatedAt);

    private static AchievementAdminSummary ToAdminSummary(Achievement item, int unlockedByUsersCount) => new(
        item.Id, item.Name, item.Description, item.Category, item.Tier, item.IconUrl, item.IconEmoji,
        item.CriteriaType, item.CriteriaValue, item.TriggerType, item.TriggerValue, item.IsRepeatable,
        item.XPReward, item.IsActive, item.SortOrder, item.CreatedAt, item.UpdatedAt, unlockedByUsersCount);

    private static GamificationSummary ToSummary(UserGamification item) => new(
        item.UserId, item.TotalXP, item.CurrentLevel, item.CurrentLevelXP, item.NextLevelXP,
        item.LevelTitle, item.LevelIcon, item.CurrentStreak, item.LongestStreak, item.LastActivityDate,
        item.StreakFreezeCount, item.TotalActivitiesCompleted, item.TotalReadingMinutes,
        item.CreatedAt, item.UpdatedAt);

    private static decimal GetValue(LeaderboardRow item, string type) => type.ToLowerInvariant() switch
    {
        "currentlevel" => item.CurrentLevel,
        "currentstreak" => item.CurrentStreak,
        "longeststreak" => item.LongestStreak,
        "totalactivities" => item.TotalActivitiesCompleted,
        "readingminutes" => item.TotalReadingMinutes,
        _ => item.TotalXP
    };

    private sealed record LeaderboardRow(
        Guid UserId,
        long TotalXP,
        int CurrentLevel,
        int CurrentStreak,
        int LongestStreak,
        int TotalActivitiesCompleted,
        int TotalReadingMinutes,
        string LevelTitle,
        string LevelIcon);
}
