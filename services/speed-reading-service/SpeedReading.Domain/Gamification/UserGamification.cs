using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Gamification;

public sealed class UserGamification : Entity
{
    private UserGamification()
    {
    }

    public Guid UserId { get; private set; }
    public long TotalXP { get; private set; }
    public int CurrentLevel { get; private set; }
    public int CurrentLevelXP { get; private set; }
    public int NextLevelXP { get; private set; }
    public string LevelTitle { get; private set; } = string.Empty;
    public string LevelIcon { get; private set; } = string.Empty;
    public int CurrentStreak { get; private set; }
    public int LongestStreak { get; private set; }
    public DateTime? LastActivityDate { get; private set; }
    public int StreakFreezeCount { get; private set; }
    public int TotalActivitiesCompleted { get; private set; }
    public int TotalReadingMinutes { get; private set; }
    public int MaxWPM { get; private set; }
    public decimal MaxComprehensionScore { get; private set; }
    public int TotalExercisesCompleted { get; private set; }
    public int TotalReadingSessionsCompleted { get; private set; }
    public string CompletedExerciseTypesJson { get; private set; } = "[]";
    public int MaxRSVPWPM { get; private set; }
    public decimal MaxRSVPComprehension { get; private set; }
    public int TotalVocabularyWordsLearned { get; private set; }
    public int MaxVocabularyBoxReached { get; private set; }
    public int TotalVocabularyQuestionsAnswered { get; private set; }
    public int VocabularyMasteryLevel { get; private set; }
    public int MaxVocabularyStreak { get; private set; }
    public string LearnedVocabularyCategoriesJson { get; private set; } = "[]";
    public string LearnedVocabularyCategoriesMapJson { get; private set; } = "{}";
    public string LearnedVocabularyDifficultiesJson { get; private set; } = "{}";
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static UserGamification CreateDefault(Guid id, Guid userId, DateTime createdAt, string? createdBy)
    {
        if (id == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Gamification identifiers are required.");
        var stats = new UserGamification
        {
            Id = id,
            UserId = userId,
            CurrentLevel = 1,
            CurrentLevelXP = 0,
            NextLevelXP = 100,
            LevelTitle = GamificationRules.GetLevelTitle(1),
            LevelIcon = GamificationRules.GetLevelIcon(1),
            StreakFreezeCount = 3,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy
        };
        return stats;
    }

    public static UserGamification Import(
        Guid id,
        Guid userId,
        long totalXp,
        int currentLevel,
        int currentLevelXp,
        int nextLevelXp,
        string levelTitle,
        string levelIcon,
        int currentStreak,
        int longestStreak,
        DateTime? lastActivityDate,
        int streakFreezeCount,
        int totalActivitiesCompleted,
        int totalReadingMinutes,
        int maxWpm,
        decimal maxComprehensionScore,
        int totalExercisesCompleted,
        int totalReadingSessionsCompleted,
        string completedExerciseTypesJson,
        int maxRsvpWpm,
        decimal maxRsvpComprehension,
        int totalVocabularyWordsLearned,
        int maxVocabularyBoxReached,
        int totalVocabularyQuestionsAnswered,
        int vocabularyMasteryLevel,
        int maxVocabularyStreak,
        string learnedVocabularyCategoriesJson,
        string learnedVocabularyCategoriesMapJson,
        string learnedVocabularyDifficultiesJson,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        var stats = CreateDefault(id, userId, createdAt, createdBy);
        stats.TotalXP = Math.Max(totalXp, 0);
        stats.CurrentLevel = Math.Max(currentLevel, 1);
        stats.CurrentLevelXP = Math.Max(currentLevelXp, 0);
        stats.NextLevelXP = Math.Max(nextLevelXp, 0);
        stats.LevelTitle = levelTitle;
        stats.LevelIcon = levelIcon;
        stats.CurrentStreak = Math.Max(currentStreak, 0);
        stats.LongestStreak = Math.Max(longestStreak, 0);
        stats.LastActivityDate = lastActivityDate.HasValue ? EnsureUtc(lastActivityDate.Value) : null;
        stats.StreakFreezeCount = Math.Max(streakFreezeCount, 0);
        stats.TotalActivitiesCompleted = Math.Max(totalActivitiesCompleted, 0);
        stats.TotalReadingMinutes = Math.Max(totalReadingMinutes, 0);
        stats.MaxWPM = Math.Max(maxWpm, 0);
        stats.MaxComprehensionScore = Math.Clamp(maxComprehensionScore, 0, 100);
        stats.TotalExercisesCompleted = Math.Max(totalExercisesCompleted, 0);
        stats.TotalReadingSessionsCompleted = Math.Max(totalReadingSessionsCompleted, 0);
        stats.CompletedExerciseTypesJson = completedExerciseTypesJson;
        stats.MaxRSVPWPM = Math.Max(maxRsvpWpm, 0);
        stats.MaxRSVPComprehension = Math.Clamp(maxRsvpComprehension, 0, 100);
        stats.TotalVocabularyWordsLearned = Math.Max(totalVocabularyWordsLearned, 0);
        stats.MaxVocabularyBoxReached = Math.Max(maxVocabularyBoxReached, 0);
        stats.TotalVocabularyQuestionsAnswered = Math.Max(totalVocabularyQuestionsAnswered, 0);
        stats.VocabularyMasteryLevel = Math.Max(vocabularyMasteryLevel, 0);
        stats.MaxVocabularyStreak = Math.Max(maxVocabularyStreak, 0);
        stats.LearnedVocabularyCategoriesJson = learnedVocabularyCategoriesJson;
        stats.LearnedVocabularyCategoriesMapJson = learnedVocabularyCategoriesMapJson;
        stats.LearnedVocabularyDifficultiesJson = learnedVocabularyDifficultiesJson;
        stats.IsDeleted = isDeleted;
        stats.DeletedAt = deletedAt.HasValue ? EnsureUtc(deletedAt.Value) : null;
        stats.DeletedBy = deletedBy;
        stats.UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null;
        stats.UpdatedBy = updatedBy;
        return stats;
    }

    public int AwardXp(int amount, Guid actorId, DateTime at)
    {
        if (amount < 0 || actorId == Guid.Empty)
            throw new ArgumentException("XP award is invalid.");
        TotalXP = checked(TotalXP + amount);
        RecalculateLevel();
        Touch(actorId, at);
        return CurrentLevel;
    }

    public void UpdateStreak(DateTime activityDate, int durationMinutes, Guid actorId, DateTime at)
    {
        if (durationMinutes < 0 || actorId == Guid.Empty)
            throw new ArgumentException("Gamification streak update is invalid.");
        CurrentStreak = GamificationRules.CalculateNextStreak(
            LastActivityDate,
            activityDate,
            CurrentStreak);
        LongestStreak = Math.Max(LongestStreak, CurrentStreak);
        TotalReadingMinutes = checked(TotalReadingMinutes + durationMinutes);
        LastActivityDate = EnsureUtc(activityDate);
        Touch(actorId, at);
    }

    public void UseStreakFreeze(Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty || StreakFreezeCount <= 0)
            throw new InvalidOperationException("No streak freezes available.");
        StreakFreezeCount--;
        LastActivityDate = EnsureUtc(at);
        Touch(actorId, at);
    }

    public void RecalculateLevel()
    {
        CurrentLevel = GamificationRules.CalculateLevel(TotalXP);
        CurrentLevelXP = GamificationRules.GetCurrentLevelXp(TotalXP, CurrentLevel);
        NextLevelXP = 100;
        LevelTitle = GamificationRules.GetLevelTitle(CurrentLevel);
        LevelIcon = GamificationRules.GetLevelIcon(CurrentLevel);
    }

    public void MarkUpdated(Guid actorId, DateTime at) => Touch(actorId, at);

    private void Touch(Guid actorId, DateTime at)
    {
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
