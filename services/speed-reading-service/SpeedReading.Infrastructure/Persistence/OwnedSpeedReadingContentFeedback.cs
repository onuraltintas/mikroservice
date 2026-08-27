using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ContentFeedback;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingContentFeedback(OwnedSpeedReadingDbContext db)
    : ISpeedReadingContentFeedback
{
    public async Task<Guid> SaveFeedbackAsync(
        Guid userId,
        SaveContentFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var contentType = NormalizeContentType(request.ContentType);
        var now = DateTime.UtcNow;
        var feedback = new LegacyUserContentFeedback
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContentId = request.ContentId,
            ContentType = contentType,
            Rating = request.Rating,
            IsLiked = request.IsLiked,
            IsBookmarked = request.IsBookmarked,
            SkipReason = request.SkipReason,
            CompletionRate = Math.Clamp(request.CompletionRate, 0, 100),
            TimeSpentSeconds = Math.Max(request.TimeSpentSeconds, 0),
            ExpectedTimeSeconds = Math.Max(request.ExpectedTimeSeconds, 0),
            ComprehensionScore = NormalizeScore(request.ComprehensionScore),
            ExerciseScore = NormalizeScore(request.ExerciseScore),
            RetryCount = Math.Max(request.RetryCount, 0),
            InteractionCount = Math.Max(request.InteractionCount, 0),
            PauseCount = Math.Max(request.PauseCount, 0),
            AbandonedAtPercentage = request.AbandonedAtPercentage is null
                ? null
                : Math.Clamp(request.AbandonedAtPercentage.Value, 0, 100),
            SessionDate = now,
            TimeOfDay = now.Hour,
            DeviceType = string.IsNullOrWhiteSpace(request.DeviceType) ? "Unknown" : request.DeviceType.Trim(),
            ContentCategory = request.ContentCategory,
            ContentDifficultyLevel = request.ContentDifficultyLevel,
            CreatedAt = now,
            CreatedBy = userId
        };

        db.ContentFeedbacks.Add(feedback);
        await db.SaveChangesAsync(cancellationToken);
        return feedback.Id;
    }

    public async Task<ContentFeedbackAnalyticsSummary> GetAnalyticsAsync(
        Guid userId,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        var query = db.ContentFeedbacks
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var normalizedType = NormalizeContentType(contentType);
            query = query.Where(item => item.ContentType == normalizedType);
        }

        var feedbacks = await query.ToListAsync(cancellationToken);
        var categories = feedbacks
            .Where(item => !string.IsNullOrWhiteSpace(item.ContentCategory))
            .GroupBy(item => item.ContentCategory!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FeedbackCategorySummary(
                group.Key,
                group.Count(),
                Math.Round(group.Average(ScoreForFeedback), 1)))
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.AverageScore)
            .Take(10)
            .ToList();
        var optimalHours = feedbacks
            .GroupBy(item => item.TimeOfDay)
            .Select(group => new FeedbackOptimalHourSummary(
                group.Key,
                Math.Round(group.Average(ScoreForFeedback), 1),
                group.Count()))
            .OrderByDescending(item => item.AverageScore)
            .ThenByDescending(item => item.SessionCount)
            .Take(10)
            .ToList();
        var weakAreas = categories
            .Where(item => item.AverageScore < 70)
            .Select(item => new FeedbackWeakAreaSummary(
                item.Category,
                item.AverageScore,
                item.Count,
                "Bu kategoride daha kısa aralıklarla tekrar yaparak performansınızı artırın.",
                item.AverageScore < 50 ? "High" : "Medium"))
            .ToList();

        return new ContentFeedbackAnalyticsSummary(
            feedbacks.Count,
            AverageOrZero(feedbacks.Select(item => item.CompletionRate)),
            AverageOrZero(feedbacks.Select(ScoreForFeedback)),
            AverageOrZero(feedbacks.Select(EngagementScore)),
            feedbacks.Count(item => item.IsLiked),
            feedbacks.Count(item => item.IsBookmarked),
            feedbacks.Count(item => !string.IsNullOrWhiteSpace(item.SkipReason)),
            categories,
            optimalHours,
            feedbacks
                .GroupBy(item => item.ContentType)
                .ToDictionary(group => group.Key, group => group.Count()),
            weakAreas);
    }

    public async Task<IReadOnlyList<RecommendedContentSummary>> GetRecommendedContentsAsync(
        Guid userId,
        string contentType,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeContentType(contentType);
        var currentLevel = await db.UserProfiles
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive)
            .Select(item => (int?)item.CurrentLevel)
            .FirstOrDefaultAsync(cancellationToken) ?? 1;
        currentLevel = Math.Clamp(currentLevel, 1, 10);
        var boundedLimit = Math.Clamp(limit, 1, 50);

        if (normalizedType == "ReadingText")
        {
            var texts = await db.ReadingTexts
                .AsNoTracking()
                .Where(item => !item.IsDeleted && item.IsActive
                    && item.DifficultyLevel >= currentLevel - 1
                    && item.DifficultyLevel <= currentLevel + 1)
                .OrderByDescending(item => item.AverageComprehensionScore)
                .Take(boundedLimit)
                .ToListAsync(cancellationToken);

            return texts.Select(text => new RecommendedContentSummary(
                text.Id,
                text.Title,
                text.Content,
                normalizedType,
                text.Category,
                text.DifficultyLevel,
                Math.Round(Math.Clamp(1m - Math.Abs(text.DifficultyLevel - currentLevel) / 3m, 0, 1) * 100, 1),
                $"Seviye {text.DifficultyLevel} metni mevcut seviyenize ({currentLevel}) uygun."))
                .ToList();
        }

        if (normalizedType == "Exercise")
        {
            return await db.Exercises
                .AsNoTracking()
                .Where(item => !item.IsDeleted
                    && item.DifficultyLevel >= currentLevel - 1
                    && item.DifficultyLevel <= currentLevel + 1)
                .OrderBy(item => item.DifficultyLevel)
                .ThenBy(item => item.Title)
                .Take(boundedLimit)
                .Select(item => new RecommendedContentSummary(
                    item.Id,
                    item.Title,
                    item.Description,
                    normalizedType,
                    null,
                    item.DifficultyLevel,
                    Math.Round(Math.Clamp(1m - Math.Abs(item.DifficultyLevel - currentLevel) / 3m, 0, 1) * 100, 1),
                    $"Seviye {item.DifficultyLevel} egzersizi mevcut seviyenize ({currentLevel}) uygun."))
                .ToListAsync(cancellationToken);
        }

        return await db.ProgramTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Take(boundedLimit)
            .Select(item => new RecommendedContentSummary(
                item.Id,
                item.Name,
                item.Description,
                "ProgramTemplate",
                null,
                item.InitialDifficultyLevel,
                50,
                "Aktif program şablonu"))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetOptimalStudyHoursAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var analytics = await GetAnalyticsAsync(userId, null, cancellationToken);
        return analytics.OptimalHours
            .OrderByDescending(item => item.AverageScore)
            .ThenByDescending(item => item.SessionCount)
            .Take(3)
            .Select(item => item.Hour)
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetContentsNeedingRetryAsync(
        Guid userId,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeContentType(contentType);
        return await db.ContentFeedbacks
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && !item.IsDeleted
                && item.ContentType == normalizedType
                && (item.CompletionRate < 70
                    || (item.ComprehensionScore.HasValue && item.ComprehensionScore < 60)
                    || (item.ExerciseScore.HasValue && item.ExerciseScore < 60)
                    || item.RetryCount > 0))
            .Select(item => item.ContentId)
            .Distinct()
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateExplicitFeedbackAsync(
        Guid userId,
        Guid contentId,
        string contentType,
        UpdateContentFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeContentType(contentType);
        var feedback = await db.ContentFeedbacks
            .Where(item => item.UserId == userId
                && item.ContentId == contentId
                && item.ContentType == normalizedType
                && !item.IsDeleted)
            .OrderByDescending(item => item.SessionDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (feedback is null)
            return false;

        if (request.Rating.HasValue)
            feedback.Rating = Math.Clamp(request.Rating.Value, 1, 5);
        if (request.IsLiked.HasValue)
            feedback.IsLiked = request.IsLiked.Value;
        if (request.IsBookmarked.HasValue)
            feedback.IsBookmarked = request.IsBookmarked.Value;

        feedback.UpdatedAt = DateTime.UtcNow;
        feedback.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeContentType(string? contentType) => (contentType ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "readingtext" => "ReadingText",
        "exercise" => "Exercise",
        "programtemplate" or "trainingseries" => "ProgramTemplate",
        _ => throw new ArgumentException("Unsupported content type.", nameof(contentType))
    };

    private static decimal? NormalizeScore(decimal? score) => score is null ? null : Math.Clamp(score.Value, 0, 100);

    private static decimal ScoreForFeedback(LegacyUserContentFeedback feedback) =>
        feedback.ComprehensionScore ?? feedback.ExerciseScore ??
        (feedback.Rating.HasValue ? feedback.Rating.Value / 5m * 100 : feedback.CompletionRate);

    private static decimal EngagementScore(LegacyUserContentFeedback feedback)
    {
        var completion = Math.Clamp(feedback.CompletionRate, 0, 100);
        var interaction = feedback.InteractionCount > 0 ? 100m : 0m;
        var pause = feedback.PauseCount == 0 ? 100m : Math.Max(0, 100m - feedback.PauseCount * 10m);
        return completion * 0.6m + interaction * 0.2m + pause * 0.2m;
    }

    private static decimal AverageOrZero(IEnumerable<decimal> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 ? 0 : Math.Round(materialized.Average(), 1);
    }
}
