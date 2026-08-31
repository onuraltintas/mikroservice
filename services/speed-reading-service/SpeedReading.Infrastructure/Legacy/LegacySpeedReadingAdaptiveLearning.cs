using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.AdaptiveLearning;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingAdaptiveLearning(SpeedReadingDbContext db)
    : ISpeedReadingAdaptiveLearning
{
    public async Task<AdaptiveProfileSummary> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(userId, cancellationToken);
        return BuildProfile(snapshot);
    }

    public async Task UpdateProfileSettingsAsync(
        Guid userId,
        UpdateAdaptiveProfileSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .SingleOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
        if (user is null)
        {
            user = new LegacyUser
            {
                Id = userId,
                IsDeleted = false
            };
            db.Users.Add(user);
        }

        user.CurrentLevel = request.CurrentLevel;
        user.TargetWPM = request.TargetWPM;
        user.TargetComprehension = request.TargetComprehension;
        user.DailyGoalMinutes = request.DailyGoalMinutes;
        user.AgeGroupConfigurationId = request.AgeGroupConfigurationId ?? user.AgeGroupConfigurationId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdaptiveDashboardSummary> GetDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(userId, cancellationToken);
        var profile = BuildProfile(snapshot);
        var goal = await GetOrCreateDailyGoalAsync(userId, snapshot.User, cancellationToken);
        var todayGoal = await BuildGoalSummaryAsync(goal, snapshot, cancellationToken);
        var recentResults = snapshot.Activities
            .OrderByDescending(item => item.CompletedAt)
            .Take(5)
            .Select(item => new AdaptiveRecentResult(item.Wpm, item.ComprehensionScore, item.CompletedAt))
            .ToList();

        return new AdaptiveDashboardSummary(
            profile,
            todayGoal,
            recentResults,
            profile.CurrentStreak,
            profile.TotalMinutesSpent,
            profile.TotalReadingSessions + profile.TotalExerciseSessions,
            profile.AverageComprehension);
    }

    public async Task<IReadOnlyList<AdaptiveWeakAreaSummary>> GetWeakAreasAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(userId, cancellationToken);
        var profile = BuildProfile(snapshot);
        var storedWeakAreas = ParseLevels(snapshot.Profile?.WeakAreas);
        var levels = storedWeakAreas.Count > 0
            ? storedWeakAreas
            : profile.BloomPerformance
                .Where(item => item.Value > 0 && item.Value < 70)
                .Select(item => item.Key)
                .ToList();

        return levels
            .Distinct()
            .Where(level => level is >= 1 and <= 6)
            .OrderBy(level => level)
            .Select(level =>
            {
                var score = profile.BloomPerformance.GetValueOrDefault(level);
                var recommendation = score <= 0
                    ? "Bu bilişsel seviyede daha fazla soru çözerek veri oluşturun."
                    : $"Bu seviyedeki sorularda doğruluk oranınızı artırmaya odaklanın (%{score:0.#}).";
                return new AdaptiveWeakAreaSummary(
                    level,
                    BloomLevelName(level),
                    score,
                    recommendation);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AdaptiveContentRecommendationSummary>> GetRecommendationsAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        var boundedCount = Math.Clamp(count, 1, 50);
        var recommendations = await db.ContentRecommendations
            .AsNoTracking()
            .Where(item => item.StudentId == userId && !item.IsDeleted)
            .OrderByDescending(item => item.ConfidenceScore)
            .ThenByDescending(item => item.CreatedAt)
            .Take(boundedCount)
            .ToListAsync(cancellationToken);

        var readingTextIds = recommendations.Select(item => item.ReadingTextId).Distinct().ToList();
        var texts = await db.ReadingTexts
            .AsNoTracking()
            .Where(item => readingTextIds.Contains(item.Id) && !item.IsDeleted && item.IsActive)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var completedTextIds = await db.StudentExerciseResults
            .AsNoTracking()
            .Where(item => item.StudentId == userId
                && !item.IsDeleted
                && item.ReadingTextId.HasValue
                && readingTextIds.Contains(item.ReadingTextId.Value))
            .Select(item => item.ReadingTextId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var startedTextIds = await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && !item.IsDeleted
                && readingTextIds.Contains(item.ReadingTextId))
            .Select(item => item.ReadingTextId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return recommendations
            .Where(item => texts.ContainsKey(item.ReadingTextId))
            .Select(item =>
            {
                var text = texts[item.ReadingTextId];
                var isCompleted = completedTextIds.Contains(item.ReadingTextId);
                var isStarted = isCompleted || startedTextIds.Contains(item.ReadingTextId);
                return new AdaptiveContentRecommendationSummary(
                    item.Id,
                    item.ReadingTextId,
                    text.Title,
                    text.Category,
                    text.DifficultyLevel,
                    text.WordCount,
                    NormalizeRecommendationScore(item.ConfidenceScore),
                    item.RecommendationReason ?? string.Empty,
                    "DifficultyProgression",
                    false,
                    isStarted,
                    isCompleted,
                    item.CreatedAt,
                    item.CreatedAt.AddDays(7));
            })
            .ToList();
    }

    public async Task<AdaptiveDailyGoalSummary> GetDailyGoalAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(userId, cancellationToken);
        var goal = await GetOrCreateDailyGoalAsync(userId, snapshot.User, cancellationToken);
        return await BuildGoalSummaryAsync(goal, snapshot, cancellationToken);
    }

    public async Task UpdateAfterSessionAsync(
        Guid userId,
        UpdateAfterAdaptiveSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.StudentLearningProfiles
            .FirstOrDefaultAsync(item => item.StudentId == userId && !item.IsDeleted, cancellationToken);

        if (profile is null)
        {
            profile = new LegacyStudentLearningProfile
            {
                Id = Guid.NewGuid(),
                StudentId = userId,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };
            db.StudentLearningProfiles.Add(profile);
        }

        if (request.BloomAnswers is not null)
        {
            var weakLevels = request.BloomAnswers
                .Where(item => !item.Value && item.Key is >= 1 and <= 6)
                .Select(item => item.Key)
                .Distinct()
                .OrderBy(item => item)
                .ToArray();
            profile.WeakAreas = JsonSerializer.Serialize(weakLevels);
        }

        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdaptiveDailyGoalSummary> UpdateDailyProgressAsync(
        Guid userId,
        UpdateAdaptiveDailyProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MinutesSpent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MinutesSpent));
        }

        var snapshot = await LoadSnapshotAsync(userId, cancellationToken);
        var goal = await GetOrCreateDailyGoalAsync(userId, snapshot.User, cancellationToken);
        goal.ActualMinutes += request.MinutesSpent;
        goal.IsCompleted = goal.ActualMinutes >= goal.TargetMinutes;
        goal.UpdatedAt = DateTime.UtcNow;
        goal.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);

        return await BuildGoalSummaryAsync(goal, snapshot, cancellationToken);
    }

    private async Task<AdaptiveSnapshot> LoadSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await db.StudentLearningProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.StudentId == userId && !item.IsDeleted, cancellationToken);
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
        var exerciseResults = await db.StudentExerciseResults
            .AsNoTracking()
            .Where(item => item.StudentId == userId && !item.IsDeleted)
            .OrderByDescending(item => item.CompletedAt)
            .ToListAsync(cancellationToken);
        var readingSessions = await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .OrderByDescending(item => item.CompletedAt)
            .ToListAsync(cancellationToken);

        var readingTextIds = exerciseResults
            .Where(item => item.ReadingTextId.HasValue)
            .Select(item => item.ReadingTextId!.Value)
            .Concat(readingSessions.Select(item => item.ReadingTextId))
            .Distinct()
            .ToList();
        var categories = await db.ReadingTexts
            .AsNoTracking()
            .Where(item => readingTextIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Category, cancellationToken);

        var activities = exerciseResults
            .Select(item => new AdaptiveActivity(
                item.CompletedAt,
                item.RawWPM,
                item.ComprehensionScore,
                item.TimeSpentSeconds,
                item.ReadingTextId,
                false,
                item.ReadingTextId.HasValue && categories.TryGetValue(item.ReadingTextId.Value, out var exerciseCategory)
                    ? exerciseCategory
                    : null))
            .Concat(readingSessions.Select(item => new AdaptiveActivity(
                item.CompletedAt,
                item.CalculatedWPM,
                item.ComprehensionRate,
                item.ReadingTimeSeconds,
                item.ReadingTextId,
                true,
                categories.TryGetValue(item.ReadingTextId, out var readingCategory) ? readingCategory : null)))
            .OrderByDescending(item => item.CompletedAt)
            .ToList();

        return new AdaptiveSnapshot(profile, user, exerciseResults, readingSessions, activities);
    }

    private async Task<LegacyDailyGoal> GetOrCreateDailyGoalAsync(
        Guid userId,
        LegacyUser? user,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var goal = await db.DailyGoals
            .FirstOrDefaultAsync(item => item.StudentId == userId
                && item.Date >= today
                && item.Date < today.AddDays(1)
                && !item.IsDeleted, cancellationToken);

        if (goal is not null)
        {
            return goal;
        }

        goal = new LegacyDailyGoal
        {
            Id = Guid.NewGuid(),
            StudentId = userId,
            Date = today,
            TargetMinutes = user?.DailyGoalMinutes > 0 ? user.DailyGoalMinutes : 20,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        db.DailyGoals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        return goal;
    }

    private async Task<AdaptiveDailyGoalSummary> BuildGoalSummaryAsync(
        LegacyDailyGoal goal,
        AdaptiveSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var todayActivities = snapshot.Activities
            .Where(item => item.CompletedAt >= DateTime.UtcNow.Date
                && item.CompletedAt < DateTime.UtcNow.Date.AddDays(1))
            .ToList();
        var percentage = goal.TargetMinutes > 0
            ? Math.Min(100m, Math.Round((decimal)goal.ActualMinutes / goal.TargetMinutes * 100, 1))
            : 0;

        return new AdaptiveDailyGoalSummary(
            goal.Id,
            goal.StudentId,
            goal.Date,
            goal.TargetMinutes,
            2,
            3,
            goal.ActualMinutes,
            todayActivities.Count(item => item.IsReadingSession),
            todayActivities.Count(item => !item.IsReadingSession),
            goal.IsCompleted,
            goal.IsCompleted ? goal.UpdatedAt : null,
            percentage,
            percentage >= 100 ? "Harika! Günlük hedefinize ulaştınız!" :
            percentage >= 50 ? "Yarı yoldasınız, devam edin!" : "Bugün biraz okuma zamanı!",
            goal.CreatedAt,
            goal.UpdatedAt);
    }

    private static AdaptiveProfileSummary BuildProfile(AdaptiveSnapshot snapshot)
    {
        var activities = snapshot.Activities;
        var averages = activities.Count == 0
            ? (Wpm: 0m, Comprehension: 0m)
            : (Wpm: activities.Average(item => item.Wpm), Comprehension: activities.Average(item => item.ComprehensionScore));
        var activeDates = activities
            .Select(item => item.CompletedAt.Date)
            .Distinct()
            .OrderByDescending(item => item)
            .ToList();
        var currentStreak = CalculateCurrentStreak(activeDates);
        var longestStreak = CalculateLongestStreak(activeDates);
        var bloomPerformance = CalculateBloomPerformance(snapshot.ExerciseResults);
        var weakAreas = ParseLevels(snapshot.Profile?.WeakAreas);
        var categoryPreferences = activities
            .Where(item => !string.IsNullOrWhiteSpace(item.Category))
            .GroupBy(item => item.Category!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new AdaptiveProfileSummary(
            snapshot.Profile?.Id ?? Guid.Empty,
            snapshot.User?.Id ?? snapshot.Profile?.StudentId ?? Guid.Empty,
            snapshot.Profile?.ProficiencyLevel ?? "Beginner",
            Math.Clamp(snapshot.User?.CurrentLevel ?? 1, 1, 10),
            Math.Round(averages.Wpm, 1),
            Math.Round(averages.Comprehension, 1),
            snapshot.ReadingSessions.Count,
            snapshot.ExerciseResults.Count,
            currentStreak,
            longestStreak,
            activities.FirstOrDefault()?.CompletedAt,
            bloomPerformance,
            categoryPreferences,
            weakAreas,
            activities.Sum(item => item.TimeSpentSeconds) / 60,
            snapshot.Profile?.CreatedAt ?? DateTime.UtcNow,
            snapshot.Profile?.UpdatedAt);
    }

    private static Dictionary<int, decimal> CalculateBloomPerformance(
        IReadOnlyList<LegacyStudentExerciseResult> results)
    {
        var metrics = new Dictionary<int, (int Correct, int Total)>();
        foreach (var result in results)
        {
            if (string.IsNullOrWhiteSpace(result.QuestionAnswersJson))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(result.QuestionAnswersJson);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var answer in document.RootElement.EnumerateArray())
                {
                    if (!TryGetInt(answer, "BloomLevel", out var level)
                        || level is < 1 or > 6
                        || !TryGetBool(answer, "IsCorrect", out var isCorrect))
                    {
                        continue;
                    }

                    metrics.TryGetValue(level, out var current);
                    metrics[level] = (current.Correct + (isCorrect ? 1 : 0), current.Total + 1);
                }
            }
            catch (JsonException)
            {
                // Historical rows may contain engine-specific JSON. Ignore only
                // the malformed row and keep the rest of the profile usable.
            }
        }

        return metrics.ToDictionary(
            item => item.Key,
            item => Math.Round((decimal)item.Value.Correct / item.Value.Total * 100, 1));
    }

    private static List<int> ParseLevels(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<int[]>(value)?.Where(level => level is >= 1 and <= 6).ToList() ?? [];
        }
        catch (JsonException)
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(item => int.TryParse(item.Trim(), out var level) ? level : 0)
                .Where(level => level is >= 1 and <= 6)
                .Distinct()
                .ToList();
        }
    }

    private static int CalculateCurrentStreak(IReadOnlyList<DateTime> activeDates)
    {
        if (activeDates.Count == 0 || activeDates[0] != DateTime.UtcNow.Date)
        {
            return 0;
        }

        var streak = 1;
        for (var index = 1; index < activeDates.Count; index++)
        {
            if (activeDates[index] != activeDates[index - 1].AddDays(-1))
            {
                break;
            }

            streak++;
        }

        return streak;
    }

    private static int CalculateLongestStreak(IReadOnlyList<DateTime> activeDates)
    {
        if (activeDates.Count == 0)
        {
            return 0;
        }

        var longest = 1;
        var current = 1;
        for (var index = 1; index < activeDates.Count; index++)
        {
            if (activeDates[index] == activeDates[index - 1].AddDays(-1))
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 1;
            }
        }

        return longest;
    }

    private static decimal NormalizeRecommendationScore(decimal score) =>
        Math.Clamp(score <= 1 ? score * 100 : score, 0, 100);

    private static string BloomLevelName(int level) => level switch
    {
        1 => "Hatırlama",
        2 => "Anlama",
        3 => "Uygulama",
        4 => "Analiz",
        5 => "Değerlendirme",
        6 => "Yaratma",
        _ => $"Seviye {level}"
    };

    private static bool TryGetInt(JsonElement element, string name, out int value)
    {
        value = 0;
        if (element.TryGetProperty(name, out var property) && property.TryGetInt32(out value))
        {
            return true;
        }

        var lowerName = char.ToLowerInvariant(name[0]) + name[1..];
        return element.TryGetProperty(lowerName, out property) && property.TryGetInt32(out value);
    }

    private static bool TryGetBool(JsonElement element, string name, out bool value)
    {
        if (element.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        var lowerName = char.ToLowerInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(lowerName, out property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private sealed record AdaptiveSnapshot(
        LegacyStudentLearningProfile? Profile,
        LegacyUser? User,
        List<LegacyStudentExerciseResult> ExerciseResults,
        List<LegacyReadingSession> ReadingSessions,
        List<AdaptiveActivity> Activities);

    private sealed record AdaptiveActivity(
        DateTime CompletedAt,
        decimal Wpm,
        decimal ComprehensionScore,
        int TimeSpentSeconds,
        Guid? ReadingTextId,
        bool IsReadingSession,
        string? Category);
}
