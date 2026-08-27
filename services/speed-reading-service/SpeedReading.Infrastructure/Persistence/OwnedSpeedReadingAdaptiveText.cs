using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.AdaptiveText;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingAdaptiveText(OwnedSpeedReadingDbContext db)
    : ISpeedReadingAdaptiveText
{
    public async Task<IReadOnlyList<AdaptiveTextRecommendationSummary>> GetRecommendationsAsync(
        Guid studentId,
        int count,
        string? selectionCriteria,
        CancellationToken cancellationToken = default)
    {
        var studentProfile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == studentId && item.IsActive, cancellationToken);
        if (studentProfile is null)
            return [];

        var profile = await db.AdaptiveReadingProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.StudentId == studentId && !item.IsDeleted, cancellationToken);
        var criteria = ParseCriteria(selectionCriteria);
        var query = db.ReadingTexts
            .AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(criteria.Category))
            query = query.Where(item => item.Category == criteria.Category);
        if (criteria.DifficultyLevel.HasValue)
            query = query.Where(item => item.DifficultyLevel == criteria.DifficultyLevel.Value);
        if (criteria.MinWordCount.HasValue)
            query = query.Where(item => item.WordCount >= criteria.MinWordCount.Value);
        if (criteria.MaxWordCount.HasValue)
            query = query.Where(item => item.WordCount <= criteria.MaxWordCount.Value);
        if (criteria.TargetAgeGroupId.HasValue)
            query = query.Where(item => item.TargetAgeGroupId == criteria.TargetAgeGroupId.Value);

        var boundedCount = Math.Clamp(count, 1, 50);
        var texts = await query
            .OrderByDescending(item => item.AverageComprehensionScore)
            .ThenBy(item => item.DifficultyLevel)
            .Take(boundedCount * 10)
            .ToListAsync(cancellationToken);
        var preferredCategories = profile?.PreferredCategories ?? [];
        return texts
            .Where(text => criteria.Tags is null || criteria.Tags.Count == 0 ||
                criteria.Tags.All(tag => text.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(tag, StringComparer.OrdinalIgnoreCase)))
            .Select(text => ScoreText(
                text,
                Math.Clamp(studentProfile.CurrentLevel, 1, 10),
                preferredCategories,
                profile?.AverageReadingSpeed ?? 0))
            .OrderByDescending(item => item.TotalScore)
            .ThenByDescending(item => item.ConfidenceScore)
            .Take(boundedCount)
            .ToList();
    }

    public async Task<AdaptiveTextRecommendationSummary?> GetBestMatchAsync(
        Guid studentId,
        string? selectionCriteria,
        CancellationToken cancellationToken = default) =>
        (await GetRecommendationsAsync(studentId, 1, selectionCriteria, cancellationToken)).FirstOrDefault();

    public async Task<AdaptiveStudentReadingProfileSummary?> GetProfileAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        await db.AdaptiveReadingProfiles
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

    public async Task<AdaptiveStudentReadingProfileSummary> UpdateProfileAsync(
        Guid studentId,
        UpdateAdaptiveTextProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessions = await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == studentId)
            .ToListAsync(cancellationToken);
        var textIds = sessions.Select(item => item.ReadingTextId).Distinct().ToArray();
        var texts = await db.ReadingTexts
            .AsNoTracking()
            .Where(item => textIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var userProfile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == studentId && item.IsActive, cancellationToken);
        var profile = await db.AdaptiveReadingProfiles
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
            db.AdaptiveReadingProfiles.Add(profile);
        }

        profile.CurrentReadingLevel = Math.Clamp(userProfile?.CurrentLevel ?? profile.CurrentReadingLevel, 1, 10);
        profile.AverageComprehensionScore = sessions.Count > 0
            ? Math.Round(sessions.Average(item => item.ComprehensionRate), 2)
            : Math.Clamp(request.ComprehensionScore, 0, 100);
        profile.AverageReadingSpeed = sessions.Count > 0
            ? Math.Round(sessions.Average(item => (decimal)item.CalculatedWpm), 2)
            : Math.Max(request.ReadingSpeedWpm, 0);
        profile.TotalTextsRead = sessions.Select(item => item.ReadingTextId).Distinct().Count();
        profile.TotalReadingTimeSeconds = sessions.Sum(item => item.ReadingTimeSeconds);
        profile.PreferredCategories = GetCategoryScores(sessions, texts)
            .Where(item => item.Score >= 70)
            .OrderByDescending(item => item.Score)
            .Take(3)
            .Select(item => item.Category)
            .ToList();
        profile.DifficultCategories = GetCategoryScores(sessions, texts)
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
            throw new KeyNotFoundException("Reading text was not found.");

        var userProfile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == studentId && item.IsActive, cancellationToken);
        if (userProfile is null)
            throw new KeyNotFoundException("Student was not found.");

        var now = DateTime.UtcNow;
        db.AdaptiveTextRecommendationHistories.Add(new LegacyTextRecommendationHistory
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ReadingTextId = request.ReadingTextId,
            RecommendedAt = now,
            WasAccepted = true,
            ConfidenceScore = Math.Clamp(request.ConfidenceScore, 0, 1),
            ReasoningJson = string.IsNullOrWhiteSpace(request.ReasoningJson) ? "{}" : request.ReasoningJson,
            StudentLevelAtTime = Math.Clamp(userProfile.CurrentLevel, 1, 10),
            CreatedAt = now,
            CreatedBy = studentId
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<(string Category, decimal Score)> GetCategoryScores(
        IReadOnlyList<SpeedReading.Domain.Sessions.ReadingSession> sessions,
        IReadOnlyDictionary<Guid, SpeedReading.Domain.Catalog.ReadingText> texts)
    {
        return texts.Values
            .GroupBy(text => text.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var score = sessions
                    .Where(session => group.Any(text => text.Id == session.ReadingTextId))
                    .Select(session => session.ComprehensionRate)
                    .DefaultIfEmpty()
                    .Average();
                return (group.Key, score);
            });
    }

    private static AdaptiveTextRecommendationSummary ScoreText(
        SpeedReading.Domain.Catalog.ReadingText text,
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
        new(profile.StudentId, profile.CurrentReadingLevel, profile.AverageComprehensionScore,
            profile.AverageReadingSpeed, profile.TotalTextsRead, profile.TotalReadingTimeSeconds,
            profile.PreferredCategories, profile.DifficultCategories, profile.LastCalculatedAt);

    private static SelectionCriteriaFilter ParseCriteria(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SelectionCriteriaFilter();
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
