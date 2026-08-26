using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Content;
using SpeedReading.Application.Gamification;
using SpeedReading.Application.Progress;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingGamification(SpeedReadingDbContext db)
    : ILegacySpeedReadingGamification
{
    public async Task<GamificationLevelUpResult> AwardXpAsync(
        Guid userId,
        AwardXpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A valid authenticated user is required.", nameof(userId));
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Amount), "XP amount must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Source))
        {
            throw new ArgumentException("Source is required.", nameof(request.Source));
        }

        var stats = await GetOrCreateStatsAsync(userId, cancellationToken);
        var oldLevel = Math.Max(stats.CurrentLevel, 1);
        var oldTier = SpeedReadingGamificationRules.GetTier(oldLevel);
        stats.TotalXP += request.Amount;
        ApplyLevel(stats);
        stats.UpdatedAt = DateTime.UtcNow;
        stats.UpdatedBy = userId;
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
        {
            return [];
        }

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
            {
                continue;
            }

            db.UserAchievements.Add(new LegacyUserAchievement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AchievementId = achievement.Id,
                UnlockedAt = now,
                IsShowcased = false,
                CreatedAt = now,
                CreatedBy = userId
            });
            stats.TotalXP += achievement.XPReward;
            unlocked.Add(ToSummary(achievement));
        }

        if (unlocked.Count == 0)
        {
            return [];
        }

        ApplyLevel(stats);
        stats.UpdatedAt = now;
        stats.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return unlocked;
    }

    public async Task<bool> UpdateShowcaseAsync(
        Guid userId,
        IReadOnlyList<Guid> achievementIds,
        CancellationToken cancellationToken = default)
    {
        if (achievementIds.Count > 5)
        {
            throw new InvalidOperationException("Cannot showcase more than 5 achievements.");
        }

        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User gamification stats not found.");
        var requestedIds = achievementIds.Distinct().ToArray();
        var unlocked = await db.UserAchievements
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (requestedIds.Any(id => unlocked.All(item => item.AchievementId != id)))
        {
            throw new InvalidOperationException("Only unlocked achievements can be showcased.");
        }

        foreach (var item in unlocked)
        {
            item.IsShowcased = false;
            item.ShowcaseOrder = null;
            if (Array.IndexOf(requestedIds, item.AchievementId) >= 0)
            {
                item.IsShowcased = true;
                item.ShowcaseOrder = Array.IndexOf(requestedIds, item.AchievementId) + 1;
                item.UpdatedAt = DateTime.UtcNow;
                item.UpdatedBy = userId;
            }
        }

        stats.UpdatedAt = DateTime.UtcNow;
        stats.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateStreakAsync(
        Guid userId,
        UpdateGamificationStreakRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DurationMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.DurationMinutes));
        }

        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User gamification stats not found.");
        var activityDate = request.ActivityDate == default ? DateTime.UtcNow : request.ActivityDate;
        stats.CurrentStreak = SpeedReadingGamificationRules.CalculateNextStreak(
            stats.LastActivityDate,
            activityDate,
            stats.CurrentStreak);
        stats.LongestStreak = Math.Max(stats.LongestStreak, stats.CurrentStreak);
        stats.TotalReadingMinutes += request.DurationMinutes;
        stats.LastActivityDate = activityDate;
        stats.UpdatedAt = DateTime.UtcNow;
        stats.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UseStreakFreezeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User gamification stats not found.");
        if (stats.StreakFreezeCount <= 0)
        {
            throw new InvalidOperationException("No streak freezes available.");
        }

        stats.StreakFreezeCount--;
        stats.LastActivityDate = DateTime.UtcNow;
        stats.UpdatedAt = DateTime.UtcNow;
        stats.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<GamificationSummary> GetUserGamificationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var item = await db.UserGamifications
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.UserId == userId && !value.IsDeleted,
                cancellationToken);

        return item is null
            ? new GamificationSummary(userId, 0, 1, 0, 100, "Başlangıç Okuyucu", "📖", 0, 0, null, 0, 0, 0, DateTime.UtcNow, null)
            : ToSummary(item);
    }

    public async Task<IReadOnlyList<AchievementSummary>> GetAchievementsAsync(
        CancellationToken cancellationToken = default) =>
        (await db.Achievements
            .AsNoTracking()
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

        return rows
            .Select(item => new UserAchievementSummary(
                item.userAchievement.Id,
                item.userAchievement.UserId,
                item.userAchievement.AchievementId,
                item.userAchievement.UnlockedAt,
                item.userAchievement.IsShowcased,
                item.userAchievement.ShowcaseOrder,
                ToSummary(item.achievement)))
            .ToList();
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
        {
            throw new BusinessRuleException("SpeedReading.LeaderboardType.Invalid", "Geçersiz liderlik tablosu türü.");
        }

        skip = Math.Clamp(skip, 0, 10_000);
        take = Math.Clamp(take, 1, 100);

        var query = db.UserGamifications
            .AsNoTracking()
            .Where(item => !item.IsDeleted);

        if (!isGlobalAdministrator)
        {
            var requestingUser = await db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == requestingUserId && !item.IsDeleted,
                    cancellationToken);

            // A non-global user without a resolved institution must not be
            // allowed to fall back to a global leaderboard.
            if (requestingUser?.InstitutionId is not { } institutionId)
            {
                return Array.Empty<LeaderboardEntry>();
            }

            query = query.Where(item => db.Users.Any(user =>
                user.Id == item.UserId
                && user.InstitutionId == institutionId
                && !user.IsDeleted));
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

        var rows = await query
            .Skip(skip)
            .Take(take)
            .Select(item => new LeaderboardRow
            {
                UserId = item.UserId,
                TotalXP = item.TotalXP,
                CurrentLevel = item.CurrentLevel,
                CurrentStreak = item.CurrentStreak,
                LongestStreak = item.LongestStreak,
                TotalActivitiesCompleted = item.TotalActivitiesCompleted,
                TotalReadingMinutes = item.TotalReadingMinutes,
                LevelTitle = item.LevelTitle,
                LevelIcon = item.LevelIcon
            })
            .ToListAsync(cancellationToken);

        var userIds = rows.Select(item => item.UserId).ToArray();
        var users = await db.Users
            .AsNoTracking()
            .Where(item => userIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

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

        var showcasedByUser = showcased
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AchievementSummary>)group
                    .Select(item => ToSummary(item.achievement))
                    .ToList());

        return rows.Select((item, index) => new LeaderboardEntry(
                item.UserId,
                users.TryGetValue(item.UserId, out var user) ? DisplayName(user) : item.UserId.ToString("D"),
                skip + index + 1,
                GetValue(item, normalizedType),
                item.CurrentLevel,
                item.LevelTitle,
                item.LevelIcon,
                showcasedByUser.TryGetValue(item.UserId, out var achievements)
                    ? achievements
                    : Array.Empty<AchievementSummary>()))
            .ToList();
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
        {
            query = query.Where(item => item.Category == category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(tier))
        {
            query = query.Where(item => item.Tier == tier.Trim());
        }

        if (isActive.HasValue)
        {
            query = query.Where(item => item.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(item => new
            {
                Achievement = item,
                UnlockedByUsersCount = db.UserAchievements.Count(userAchievement =>
                    userAchievement.AchievementId == item.Id && !userAchievement.IsDeleted)
            })
            .ToListAsync(cancellationToken);

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
        var item = await db.Achievements
            .AsNoTracking()
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
        var rows = await db.Achievements
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
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
            rows.GroupBy(item => item.Category)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
    }

    private static readonly string[] LeaderboardTypes =
    ["TotalXP", "CurrentLevel", "CurrentStreak", "LongestStreak", "TotalActivities", "ReadingMinutes"];

    private static decimal GetValue(LeaderboardRow item, string type) => type.ToLowerInvariant() switch
    {
        "currentlevel" => item.CurrentLevel,
        "currentstreak" => item.CurrentStreak,
        "longeststreak" => item.LongestStreak,
        "totalactivities" => item.TotalActivitiesCompleted,
        "readingminutes" => item.TotalReadingMinutes,
        _ => item.TotalXP
    };

    private async Task<LegacyUserGamification> GetOrCreateStatsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (stats is not null)
        {
            return stats;
        }

        stats = new LegacyUserGamification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentLevel = 1,
            CurrentLevelXP = 0,
            NextLevelXP = 100,
            LevelTitle = SpeedReadingGamificationRules.GetLevelTitle(1),
            LevelIcon = SpeedReadingGamificationRules.GetLevelIcon(1),
            StreakFreezeCount = 3,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        db.UserGamifications.Add(stats);
        return stats;
    }

    private static void ApplyLevel(LegacyUserGamification stats)
    {
        stats.CurrentLevel = SpeedReadingGamificationRules.CalculateLevel(stats.TotalXP);
        stats.CurrentLevelXP = SpeedReadingGamificationRules.GetCurrentLevelXp(
            stats.TotalXP,
            stats.CurrentLevel);
        stats.NextLevelXP = 100;
        stats.LevelTitle = SpeedReadingGamificationRules.GetLevelTitle(stats.CurrentLevel);
        stats.LevelIcon = SpeedReadingGamificationRules.GetLevelIcon(stats.CurrentLevel);
    }

    private static bool MeetsCriteria(LegacyAchievement achievement, LegacyUserGamification stats)
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
                "vocabulary_categories" => MeetsInt(
                    criteria,
                    "count",
                    JsonSerializer.Deserialize<List<string>>(stats.LearnedVocabularyCategoriesJson)?.Count ?? 0),
                "rsvp_wpm" => MeetsInt(criteria, "wpm", stats.MaxRSVPWPM),
                "rsvp_comprehension" => MeetsDecimal(criteria, "score", stats.MaxRSVPComprehension),
                "exercise_type_first" => MeetsType(criteria, stats.CompletedExerciseTypesJson),
                "exercise_variety" => MeetsInt(
                    criteria,
                    "types",
                    JsonSerializer.Deserialize<List<string>>(stats.CompletedExerciseTypesJson)?.Count ?? 0),
                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool MeetsInt(JsonElement criteria, string propertyName, int current) =>
        criteria.TryGetProperty(propertyName, out var value)
        && value.TryGetInt32(out var target)
        && current >= target;

    private static bool MeetsLong(JsonElement criteria, string propertyName, long current) =>
        criteria.TryGetProperty(propertyName, out var value)
        && value.TryGetInt64(out var target)
        && current >= target;

    private static bool MeetsDecimal(JsonElement criteria, string propertyName, decimal current) =>
        criteria.TryGetProperty(propertyName, out var value)
        && value.TryGetDecimal(out var target)
        && current >= target;

    private static bool MeetsType(JsonElement criteria, string jsonTypes)
    {
        if (!criteria.TryGetProperty("type", out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return JsonSerializer.Deserialize<List<string>>(jsonTypes)?
            .Contains(value.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static string DisplayName(LegacyUser user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName)
            ? user.UserName ?? user.Email ?? user.Id.ToString("D")
            : fullName;
    }

    internal static AchievementSummary ToSummary(LegacyAchievement item) => new(
        item.Id,
        item.Name,
        item.Description,
        item.Category,
        item.Tier,
        item.IconUrl,
        item.IconEmoji,
        item.CriteriaType,
        item.CriteriaValue,
        item.TriggerType,
        item.TriggerValue,
        item.IsRepeatable,
        item.XPReward,
        item.IsActive,
        item.SortOrder,
        item.CreatedAt);

    internal static AchievementAdminSummary ToAdminSummary(LegacyAchievement item, int unlockedByUsersCount) => new(
        item.Id,
        item.Name,
        item.Description,
        item.Category,
        item.Tier,
        item.IconUrl,
        item.IconEmoji,
        item.CriteriaType,
        item.CriteriaValue,
        item.TriggerType,
        item.TriggerValue,
        item.IsRepeatable,
        item.XPReward,
        item.IsActive,
        item.SortOrder,
        item.CreatedAt,
        item.UpdatedAt,
        unlockedByUsersCount);

    private static GamificationSummary ToSummary(LegacyUserGamification item) => new(
        item.UserId,
        item.TotalXP,
        item.CurrentLevel,
        item.CurrentLevelXP,
        item.NextLevelXP,
        item.LevelTitle,
        item.LevelIcon,
        item.CurrentStreak,
        item.LongestStreak,
        item.LastActivityDate,
        item.StreakFreezeCount,
        item.TotalActivitiesCompleted,
        item.TotalReadingMinutes,
        item.CreatedAt,
        item.UpdatedAt);

    private sealed record LeaderboardRow
    {
        public Guid UserId { get; init; }
        public long TotalXP { get; init; }
        public int CurrentLevel { get; init; }
        public int CurrentStreak { get; init; }
        public int LongestStreak { get; init; }
        public int TotalActivitiesCompleted { get; init; }
        public int TotalReadingMinutes { get; init; }
        public string LevelTitle { get; init; } = string.Empty;
        public string LevelIcon { get; init; } = string.Empty;
    }
}

internal sealed class LegacySpeedReadingGamificationAdminWriter(SpeedReadingDbContext db)
    : ISpeedReadingGamificationAdminWriter
{
    private const string CreateScope = "speed-reading.achievements.create";
    private const string UpdateScope = "speed-reading.achievements.update";
    private const string DeleteScope = "speed-reading.achievements.delete";

    public async Task<AchievementAdminSummary> CreateAchievementAsync(
        Guid actorId,
        CreateAchievementRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        var key = NormalizeKey(idempotencyKey);
        var hash = RequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(CreateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetAdminSummaryAsync(existing.ResourceId, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var achievement = new LegacyAchievement
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category.Trim(),
            Tier = request.Tier.Trim(),
            IconUrl = request.IconUrl?.Trim() ?? string.Empty,
            IconEmoji = request.IconEmoji.Trim(),
            CriteriaType = request.CriteriaType.Trim(),
            CriteriaValue = request.CriteriaValue.Trim(),
            TriggerType = request.TriggerType?.Trim(),
            TriggerValue = request.TriggerValue,
            IsRepeatable = request.IsRepeatable,
            XPReward = request.XpReward,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.Achievements.Add(achievement);
        db.IdempotencyRecords.Add(new LegacyIdempotencyRecord
        {
            Id = Guid.NewGuid(), Scope = CreateScope, Key = key, RequestHash = hash,
            ResourceId = achievement.Id, CreatedAt = now
        });
        await SaveWithReplayAsync(CreateScope, key, hash, cancellationToken);
        return await GetAdminSummaryAsync(achievement.Id, cancellationToken);
    }

    public async Task<AchievementAdminSummary> UpdateAchievementAsync(
        Guid actorId,
        Guid achievementId,
        UpdateAchievementRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        var key = NormalizeKey(idempotencyKey);
        var hash = RequestHash(actorId, UpdateScope, achievementId, request);
        var existing = await GetLedgerAsync(UpdateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetAdminSummaryAsync(existing.ResourceId, cancellationToken);
        }

        var achievement = await db.Achievements.SingleOrDefaultAsync(
            item => item.Id == achievementId && !item.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Achievement", achievementId);

        Apply(achievement, request, actorId);
        db.IdempotencyRecords.Add(new LegacyIdempotencyRecord
        {
            Id = Guid.NewGuid(), Scope = UpdateScope, Key = key, RequestHash = hash,
            ResourceId = achievement.Id, CreatedAt = DateTime.UtcNow
        });
        await SaveWithReplayAsync(UpdateScope, key, hash, cancellationToken);
        return await GetAdminSummaryAsync(achievement.Id, cancellationToken);
    }

    public async Task DeleteAchievementAsync(
        Guid actorId,
        Guid achievementId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = NormalizeKey(idempotencyKey);
        var hash = SpeedReadingRequestHasher.Create(actorId.ToString("D"), DeleteScope, achievementId.ToString("D"));
        var existing = await GetLedgerAsync(DeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return;
        }

        var achievement = await db.Achievements.SingleOrDefaultAsync(
            item => item.Id == achievementId && !item.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Achievement", achievementId);
        if (await db.UserAchievements.AnyAsync(
                item => item.AchievementId == achievementId && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "SpeedReading.Achievement.HasUnlocks",
                "Kazanımı kullanan öğrenci kayıtları varken silme yapılamaz; pasif hale getirin.");
        }

        var now = DateTime.UtcNow;
        achievement.IsDeleted = true;
        achievement.IsActive = false;
        achievement.DeletedAt = now;
        achievement.DeletedBy = actorId;
        achievement.UpdatedAt = now;
        achievement.UpdatedBy = actorId;
        db.IdempotencyRecords.Add(new LegacyIdempotencyRecord
        {
            Id = Guid.NewGuid(), Scope = DeleteScope, Key = key, RequestHash = hash,
            ResourceId = achievement.Id, CreatedAt = now
        });
        await SaveWithReplayAsync(DeleteScope, key, hash, cancellationToken);
    }

    private async Task<AchievementAdminSummary> GetAdminSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Achievements.SingleOrDefaultAsync(value => value.Id == id, cancellationToken)
            ?? throw new NotFoundException("Achievement", id);
        var unlocked = await db.UserAchievements.CountAsync(
            value => value.AchievementId == id && !value.IsDeleted,
            cancellationToken);
        return LegacySpeedReadingGamification.ToAdminSummary(item, unlocked);
    }

    private async Task<LegacyIdempotencyRecord?> GetLedgerAsync(
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        await db.IdempotencyRecords.SingleOrDefaultAsync(
            item => item.Scope == scope && item.Key == key,
            cancellationToken);

    private async Task SaveWithReplayAsync(
        string scope,
        string key,
        string hash,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords.SingleAsync(
                item => item.Scope == scope && item.Key == key,
                cancellationToken);
            EnsureReplay(concurrent, hash);
        }
    }

    private static void Apply(
        LegacyAchievement achievement,
        UpdateAchievementRequest request,
        Guid actorId)
    {
        achievement.Name = request.Name.Trim();
        achievement.Description = request.Description.Trim();
        achievement.Category = request.Category.Trim();
        achievement.Tier = request.Tier.Trim();
        achievement.IconUrl = request.IconUrl?.Trim() ?? string.Empty;
        achievement.IconEmoji = request.IconEmoji.Trim();
        achievement.CriteriaType = request.CriteriaType.Trim();
        achievement.CriteriaValue = request.CriteriaValue.Trim();
        achievement.TriggerType = request.TriggerType?.Trim();
        achievement.TriggerValue = request.TriggerValue;
        achievement.IsRepeatable = request.IsRepeatable;
        achievement.XPReward = request.XpReward;
        achievement.IsActive = request.IsActive;
        achievement.SortOrder = request.SortOrder;
        achievement.UpdatedAt = DateTime.UtcNow;
        achievement.UpdatedBy = actorId;
    }

    private static void ValidateRequest(Guid actorId, CreateAchievementRequest request, string key)
    {
        ValidateIdempotency(actorId, key);
        ValidateFields(request.Name, request.Description, request.Category, request.Tier,
            request.IconUrl, request.IconEmoji, request.CriteriaType, request.CriteriaValue,
            request.TriggerType, request.TriggerValue, request.XpReward, request.SortOrder);
    }

    private static void ValidateRequest(Guid actorId, UpdateAchievementRequest request, string key)
    {
        ValidateIdempotency(actorId, key);
        ValidateFields(request.Name, request.Description, request.Category, request.Tier,
            request.IconUrl, request.IconEmoji, request.CriteriaType, request.CriteriaValue,
            request.TriggerType, request.TriggerValue, request.XpReward, request.SortOrder);
    }

    private static void ValidateFields(
        string name,
        string description,
        string category,
        string tier,
        string? iconUrl,
        string iconEmoji,
        string criteriaType,
        string criteriaValue,
        string? triggerType,
        int? triggerValue,
        int xpReward,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new BusinessRuleException("SpeedReading.Achievement.Name.Invalid", "Kazanım adı 1-200 karakter olmalıdır.");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 500)
            throw new BusinessRuleException("SpeedReading.Achievement.Description.Invalid", "Kazanım açıklaması 1-500 karakter olmalıdır.");
        if (string.IsNullOrWhiteSpace(category) || category.Trim().Length > 50)
            throw new BusinessRuleException("SpeedReading.Achievement.Category.Invalid", "Kategori 1-50 karakter olmalıdır.");
        if (string.IsNullOrWhiteSpace(tier) || tier.Trim().Length > 50)
            throw new BusinessRuleException("SpeedReading.Achievement.Tier.Invalid", "Seviye 1-50 karakter olmalıdır.");
        if (iconUrl?.Trim().Length > 500)
            throw new BusinessRuleException("SpeedReading.Achievement.IconUrl.Invalid", "İkon adresi en fazla 500 karakter olabilir.");
        if (iconEmoji.Trim().Length > 10)
            throw new BusinessRuleException("SpeedReading.Achievement.IconEmoji.Invalid", "İkon emojisi en fazla 10 karakter olabilir.");
        if (string.IsNullOrWhiteSpace(criteriaType) || criteriaType.Trim().Length > 100)
            throw new BusinessRuleException("SpeedReading.Achievement.CriteriaType.Invalid", "Kriter tipi 1-100 karakter olmalıdır.");
        if (string.IsNullOrWhiteSpace(criteriaValue) || criteriaValue.Length > 4_000)
            throw new BusinessRuleException("SpeedReading.Achievement.CriteriaValue.Invalid", "Kriter değeri 1-4000 karakter olmalıdır.");
        if (!IsJsonObjectOrArray(criteriaValue))
            throw new BusinessRuleException("SpeedReading.Achievement.CriteriaValue.Invalid", "Kriter değeri geçerli JSON nesnesi veya dizisi olmalıdır.");
        if (triggerType?.Trim().Length > 100)
            throw new BusinessRuleException("SpeedReading.Achievement.TriggerType.Invalid", "Tetikleyici tipi en fazla 100 karakter olabilir.");
        if (triggerValue is < 0)
            throw new BusinessRuleException("SpeedReading.Achievement.TriggerValue.Invalid", "Tetikleyici değeri negatif olamaz.");
        if (xpReward is < 0 or > 1_000_000)
            throw new BusinessRuleException("SpeedReading.Achievement.XpReward.Invalid", "XP ödülü 0-1000000 aralığında olmalıdır.");
        if (sortOrder is < 0 or > 100_000)
            throw new BusinessRuleException("SpeedReading.Achievement.SortOrder.Invalid", "Sıra 0-100000 aralığında olmalıdır.");
    }

    private static bool IsJsonObjectOrArray(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateIdempotency(Guid actorId, string key)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(key)
            || key.Length is < 16 or > 128
            || key.Any(character => !(char.IsLetterOrDigit(character)
                || character is '.' or '_' or '-' or '~')))
        {
            throw new BusinessRuleException("SpeedReading.IdempotencyKey.Invalid", "Geçerli bir Idempotency-Key gereklidir.");
        }
    }

    private static string NormalizeKey(string key) => key.Trim();

    private static string RequestHash(Guid actorId, string scope, Guid resourceId, object request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"),
            scope,
            resourceId.ToString("D"),
            JsonSerializer.Serialize(request));

    private static void EnsureReplay(LegacyIdempotencyRecord record, string requestHash)
    {
        if (!record.Matches(requestHash))
            throw new BusinessRuleException(
                "SpeedReading.IdempotencyKey.Reused",
                "Idempotency-Key farklı bir istek için tekrar kullanılamaz.");
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
