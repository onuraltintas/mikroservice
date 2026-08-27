using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingAdaptiveLearningBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedAdaptiveLearningBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var sourceProfiles = await legacy.Database.SqlQueryRaw<LegacyStudentLearningProfileSource>("""
            SELECT "Id", "StudentId", "CurrentDifficultyLevel", "AverageWPM" AS "AverageWpm",
                   "AverageComprehension", "TotalReadingSessions", "TotalExerciseSessions",
                   "CurrentStreak", "LongestStreak", "LastActiveDate", "BloomPerformanceJson",
                   "CategoryPreferencesJson", "WeakAreasJson", "RecentlyReadMaterialIdsJson",
                   "TotalMinutesSpent", "CreatedAt", "UpdatedAt"
            FROM "StudentLearningProfiles"
            """).ToListAsync(cancellationToken);
        var sourceRecommendations = await legacy.Database.SqlQueryRaw<LegacyContentRecommendationSource>("""
            SELECT "Id", "StudentId", "ReadingTextId", "RecommendationScore", "Reason",
                   "RecommendationType", "IsViewed", "IsStarted", "IsCompleted", "ViewedAt",
                   "StartedAt", "CompletedAt", "CreatedAt", "ExpiresAt"
            FROM "ContentRecommendations"
            """).ToListAsync(cancellationToken);
        var sourceGoals = await legacy.Database.SqlQueryRaw<LegacyDailyGoalSource>("""
            SELECT "Id", "StudentId", "Date", "TargetMinutes", "TargetReadingSessions",
                   "TargetExercises", "ActualMinutes", "ActualReadingSessions", "ActualExercises",
                   "IsAchieved", "AchievedAt", "CreatedAt", "UpdatedAt"
            FROM "DailyGoals"
            """).ToListAsync(cancellationToken);
        var recommendationTextIds = sourceRecommendations.Select(item => item.ReadingTextId).Distinct().ToArray();
        var ownedTextIds = await owned.ReadingTexts
            .IgnoreQueryFilters()
            .Where(item => recommendationTextIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        if (sourceRecommendations.Any(item => !ownedTextIds.Contains(item.ReadingTextId)))
            throw new InvalidOperationException("Adaptive recommendation references a missing owned reading text.");

        var existingProfileIds = await owned.AdaptiveLearningProfiles
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var existingRecommendationIds = await owned.AdaptiveContentRecommendations
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var existingGoalIds = await owned.AdaptiveDailyGoals
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var imported = 0;
        foreach (var item in sourceProfiles.Where(item => existingProfileIds.Add(item.Id)))
        {
            owned.AdaptiveLearningProfiles.Add(new LegacyStudentLearningProfile
            {
                Id = item.Id,
                StudentId = item.StudentId,
                ProficiencyLevel = item.CurrentDifficultyLevel.ToString(),
                PreferredContentTypes = item.CategoryPreferencesJson,
                LearningPace = "Normal",
                WeakAreas = item.WeakAreasJson,
                StrongAreas = item.BloomPerformanceJson,
                CreatedAt = item.CreatedAt,
                CreatedBy = item.StudentId,
                UpdatedAt = item.UpdatedAt,
                IsDeleted = false
            });
            imported++;
        }
        foreach (var item in sourceRecommendations.Where(item => existingRecommendationIds.Add(item.Id)))
        {
            owned.AdaptiveContentRecommendations.Add(new LegacyContentRecommendation
            {
                Id = item.Id,
                StudentId = item.StudentId,
                ReadingTextId = item.ReadingTextId,
                ConfidenceScore = item.RecommendationScore,
                RecommendationReason = item.Reason,
                CreatedAt = item.CreatedAt,
                CreatedBy = item.StudentId,
                IsDeleted = false
            });
            imported++;
        }
        foreach (var item in sourceGoals.Where(item => existingGoalIds.Add(item.Id)))
        {
            owned.AdaptiveDailyGoals.Add(new LegacyDailyGoal
            {
                Id = item.Id,
                StudentId = item.StudentId,
                Date = item.Date,
                TargetMinutes = item.TargetMinutes,
                ActualMinutes = item.ActualMinutes,
                IsCompleted = item.IsAchieved,
                CreatedAt = item.CreatedAt,
                CreatedBy = item.StudentId,
                UpdatedAt = item.UpdatedAt,
                IsDeleted = false
            });
            imported++;
        }

        if (imported > 0)
            await owned.SaveChangesAsync(cancellationToken);

        return new OwnedAdaptiveLearningBackfillResult(
            sourceProfiles.Count + sourceRecommendations.Count + sourceGoals.Count,
            imported);
    }
}

public sealed record OwnedAdaptiveLearningBackfillResult(int SourceCount, int ImportedCount);
