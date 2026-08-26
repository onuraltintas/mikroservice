using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.AdaptiveText;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingAdaptiveText(SpeedReadingDbContext db)
    : ISpeedReadingAdaptiveText
{
    public async Task<IReadOnlyList<AdaptiveTextRecommendationSummary>> GetRecommendationsAsync(
        Guid studentId,
        int count,
        string? selectionCriteria,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == studentId && !item.IsDeleted, cancellationToken);
        if (user is null)
        {
            return [];
        }

        var profile = await db.StudentReadingProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.StudentId == studentId && !item.IsDeleted, cancellationToken);
        var criteria = ParseCriteria(selectionCriteria);
        var query = db.ReadingTexts
            .AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(criteria.Category))
        {
            query = query.Where(item => item.Category == criteria.Category);
        }

        if (criteria.DifficultyLevel.HasValue)
        {
            query = query.Where(item => item.DifficultyLevel == criteria.DifficultyLevel.Value);
        }

        if (criteria.MinWordCount.HasValue)
        {
            query = query.Where(item => item.WordCount >= criteria.MinWordCount.Value);
        }

        if (criteria.MaxWordCount.HasValue)
        {
            query = query.Where(item => item.WordCount <= criteria.MaxWordCount.Value);
        }

        if (criteria.TargetAgeGroupId.HasValue)
        {
            query = query.Where(item => item.TargetAgeGroupConfigurationId == criteria.TargetAgeGroupId.Value);
        }

        var texts = await query
            .OrderByDescending(item => item.AverageComprehensionScore)
            .ThenBy(item => item.DifficultyLevel)
            .Take(Math.Clamp(count, 1, 50) * 10)
            .ToListAsync(cancellationToken);

        var preferredCategories = profile?.PreferredCategories ?? [];
        var filteredTexts = texts
            .Where(text => criteria.Tags is null || criteria.Tags.Count == 0 ||
                criteria.Tags.All(tag => text.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(tag, StringComparer.OrdinalIgnoreCase)))
            .Select(text => ScoreText(text, user.CurrentLevel, preferredCategories, profile?.AverageReadingSpeed ?? 0))
            .OrderByDescending(item => item.TotalScore)
            .ThenByDescending(item => item.ConfidenceScore)
            .Take(Math.Clamp(count, 1, 50))
            .ToList();

        return filteredTexts;
    }

    public async Task<AdaptiveTextRecommendationSummary?> GetBestMatchAsync(
        Guid studentId,
        string? selectionCriteria,
        CancellationToken cancellationToken = default)
    {
        var recommendations = await GetRecommendationsAsync(studentId, 1, selectionCriteria, cancellationToken);
        return recommendations.FirstOrDefault();
    }

    public async Task<AdaptiveStudentReadingProfileSummary?> GetProfileAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        return await db.StudentReadingProfiles
            .AsNoTracking()
            .Where(item => item.StudentId == studentId && !item.IsDeleted)
            .Select(item => new AdaptiveStudentReadingProfileSummary(
                item.StudentId,
                item.CurrentReadingLevel,
                item.AverageComprehensionScore,
                item.AverageReadingSpeed,
                item.TotalTextsRead,
                item.TotalReadingTimeSeconds,
                item.PreferredCategories,
                item.DifficultCategories,
                item.LastCalculatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AdaptiveStudentReadingProfileSummary> UpdateProfileAsync(
        Guid studentId,
        UpdateAdaptiveTextProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessions = await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == studentId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var textIds = sessions.Select(item => item.ReadingTextId).Distinct().ToList();
        var texts = await db.ReadingTexts
            .AsNoTracking()
            .Where(item => textIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == studentId && !item.IsDeleted, cancellationToken);
        var profile = await db.StudentReadingProfiles
            .FirstOrDefaultAsync(item => item.StudentId == studentId && !item.IsDeleted, cancellationToken);

        if (profile is null)
        {
            profile = new LegacyStudentReadingProfile
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                CreatedBy = studentId,
                CreatedAt = DateTime.UtcNow
            };
            db.StudentReadingProfiles.Add(profile);
        }

        profile.CurrentReadingLevel = Math.Clamp(user?.CurrentLevel ?? profile.CurrentReadingLevel, 1, 10);
        profile.AverageComprehensionScore = sessions.Count > 0
            ? Math.Round(sessions.Average(item => item.ComprehensionRate), 2)
            : request.ComprehensionScore;
        profile.AverageReadingSpeed = sessions.Count > 0
            ? Math.Round(sessions.Average(item => (decimal)item.CalculatedWPM), 2)
            : request.ReadingSpeedWpm;
        profile.TotalTextsRead = sessions.Select(item => item.ReadingTextId).Distinct().Count();
        profile.TotalReadingTimeSeconds = sessions.Sum(item => item.ReadingTimeSeconds);
        profile.PreferredCategories = texts.Values
            .GroupBy(text => text.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Category = group.Key,
                Score = sessions
                    .Where(session => group.Any(text => text.Id == session.ReadingTextId))
                    .Select(session => session.ComprehensionRate)
                    .DefaultIfEmpty()
                    .Average()
            })
            .Where(item => item.Score >= 70)
            .OrderByDescending(item => item.Score)
            .Take(3)
            .Select(item => item.Category)
            .ToList();
        profile.DifficultCategories = texts.Values
            .GroupBy(text => text.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Category = group.Key,
                Score = sessions
                    .Where(session => group.Any(text => text.Id == session.ReadingTextId))
                    .Select(session => session.ComprehensionRate)
                    .DefaultIfEmpty()
                    .Average()
            })
            .Where(item => item.Score > 0 && item.Score < 70)
            .OrderBy(item => item.Score)
            .Take(3)
            .Select(item => item.Category)
            .ToList();
        profile.LastCalculatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = studentId;
        await db.SaveChangesAsync(cancellationToken);

        return ToProfileSummary(profile);
    }

    public async Task RecordRecommendationAsync(
        Guid studentId,
        RecordAdaptiveTextRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        var textExists = await db.ReadingTexts
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.ReadingTextId && !item.IsDeleted, cancellationToken);
        if (!textExists)
        {
            throw new KeyNotFoundException("Reading text was not found.");
        }

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == studentId && !item.IsDeleted, cancellationToken);
        if (user is null)
        {
            throw new KeyNotFoundException("Student was not found.");
        }

        db.TextRecommendationHistories.Add(new LegacyTextRecommendationHistory
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ReadingTextId = request.ReadingTextId,
            RecommendedAt = DateTime.UtcNow,
            WasAccepted = true,
            ConfidenceScore = Math.Clamp(request.ConfidenceScore, 0, 1),
            ReasoningJson = string.IsNullOrWhiteSpace(request.ReasoningJson) ? "{}" : request.ReasoningJson,
            StudentLevelAtTime = Math.Clamp(user.CurrentLevel, 1, 10),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = studentId
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AdaptiveTextRecommendationSummary ScoreText(
        LegacyReadingText text,
        int currentLevel,
        IReadOnlyList<string> preferredCategories,
        decimal averageReadingSpeed)
    {
        var levelDifference = Math.Abs(text.DifficultyLevel - currentLevel);
        var levelMatch = Math.Max(0m, 1m - levelDifference / 3m);
        var comprehension = text.AverageComprehensionScore > 0
            ? Math.Clamp(text.AverageComprehensionScore / 100m, 0, 1)
            : 0.75m;
        var categoryMatch = preferredCategories.Contains(text.Category, StringComparer.OrdinalIgnoreCase) ? 1m : 0.5m;
        var totalScore = levelMatch * 0.5m + comprehension * 0.3m + categoryMatch * 0.2m;
        var confidence = Math.Round(totalScore, 2);
        var tags = text.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wpm = averageReadingSpeed > 0 ? averageReadingSpeed : 200m;

        return new AdaptiveTextRecommendationSummary(
            text.Id,
            text.Title,
            text.Content,
            text.Category,
            text.DifficultyLevel,
            text.WordCount,
            Math.Max(1, (int)Math.Ceiling(text.WordCount / wpm)),
            tags,
            text.RecommendedMinLevel,
            text.RecommendedMaxLevel,
            text.AverageComprehensionScore,
            text.TimesRead,
            Math.Round(totalScore * 100, 1),
            confidence,
            new Dictionary<string, decimal>
            {
                ["LevelMatch"] = Math.Round(levelMatch, 2),
                ["Comprehension"] = Math.Round(comprehension, 2),
                ["CategoryPreference"] = categoryMatch
            },
            $"Seviye {text.DifficultyLevel} metni, mevcut okuma seviyenize ({currentLevel}) uygun.");
    }

    private static AdaptiveStudentReadingProfileSummary ToProfileSummary(LegacyStudentReadingProfile profile) =>
        new(
            profile.StudentId,
            profile.CurrentReadingLevel,
            profile.AverageComprehensionScore,
            profile.AverageReadingSpeed,
            profile.TotalTextsRead,
            profile.TotalReadingTimeSeconds,
            profile.PreferredCategories,
            profile.DifficultCategories,
            profile.LastCalculatedAt);

    private static SelectionCriteriaFilter ParseCriteria(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SelectionCriteriaFilter();
        }

        try
        {
            return JsonSerializer.Deserialize<SelectionCriteriaFilter>(json) ?? new SelectionCriteriaFilter();
        }
        catch (JsonException)
        {
            return new SelectionCriteriaFilter();
        }
    }

    private sealed class SelectionCriteriaFilter
    {
        public string? Category { get; set; }
        public int? DifficultyLevel { get; set; }
        public int? MinWordCount { get; set; }
        public int? MaxWordCount { get; set; }
        public List<string>? Tags { get; set; }
        public Guid? TargetAgeGroupId { get; set; }
    }
}
