using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.StudentReading;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Profiles;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingStudentReading(OwnedSpeedReadingDbContext db)
    : ISpeedReadingStudentReading
{
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentLevel = await GetCurrentLevelAsync(userId, cancellationToken);
        var minLevel = Math.Max(1, currentLevel - 2);
        var maxLevel = Math.Min(10, currentLevel + 2);

        return await db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.IsActive
                && item.DifficultyLevel >= minLevel
                && item.DifficultyLevel <= maxLevel
                && item.Category != string.Empty)
            .Select(item => item.Category)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudentReadingTextSummary>> GetAvailableTextsAsync(
        Guid userId,
        string? category,
        int? minLevel,
        int? maxLevel,
        int? specificLevel,
        CancellationToken cancellationToken)
    {
        var currentLevel = await GetCurrentLevelAsync(userId, cancellationToken);
        var lowerLevel = Math.Clamp(minLevel ?? currentLevel - 2, 1, 10);
        var upperLevel = Math.Clamp(maxLevel ?? currentLevel + 2, lowerLevel, 10);

        var query = db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.IsActive
                && item.DifficultyLevel >= lowerLevel
                && item.DifficultyLevel <= upperLevel);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(item => item.Category == category);
        if (specificLevel.HasValue)
            query = query.Where(item => item.DifficultyLevel == specificLevel.Value);

        return await query
            .OrderBy(item => item.DifficultyLevel)
            .ThenBy(item => item.Title)
            .Take(50)
            .Select(item => new StudentReadingTextSummary(
                item.Id,
                item.Title,
                item.Category,
                item.DifficultyLevel,
                item.WordCount,
                item.Language))
            .ToListAsync(cancellationToken);
    }

    public async Task<StudentReadingStart?> StartAsync(
        Guid textId,
        CancellationToken cancellationToken)
    {
        var text = await db.ReadingTexts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == textId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (text is null)
            return null;

        var questions = await db.ReadingQuestions
            .AsNoTracking()
            .Where(item => item.ReadingTextId == textId && !item.IsDeleted)
            .OrderBy(item => item.OrderIndex)
            .Select(item => new StudentReadingQuestion(
                item.Id,
                item.ReadingTextId,
                item.QuestionText,
                item.Type,
                item.BloomLevel,
                item.DifficultyLevel,
                item.Explanation,
                item.OptionA,
                item.OptionB,
                item.OptionC,
                item.OptionD,
                item.CorrectAnswer,
                item.OrderIndex))
            .ToListAsync(cancellationToken);

        return new StudentReadingStart(
            text.Id,
            text.Title,
            text.Content,
            text.Category,
            text.DifficultyLevel,
            text.WordCount,
            questions);
    }

    public async Task<StudentReadingCompletion?> CompleteAsync(
        Guid userId,
        Guid textId,
        CompleteStudentReadingRequest request,
        CancellationToken cancellationToken)
    {
        var text = await db.ReadingTexts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == textId && !item.IsDeleted, cancellationToken);
        if (text is null)
            return null;

        var questions = await db.ReadingQuestions
            .AsNoTracking()
            .Where(item => item.ReadingTextId == textId && !item.IsDeleted)
            .Select(item => new { item.Id, item.CorrectAnswer })
            .ToListAsync(cancellationToken);
        var answers = request.Answers ?? [];
        var correctAnswers = answers
            .Join(questions, answer => answer.QuestionId, question => question.Id, (answer, question) =>
                string.Equals(answer.SelectedAnswer?.Trim(), question.CorrectAnswer?.Trim(), StringComparison.OrdinalIgnoreCase))
            .Count(isCorrect => isCorrect);
        var timeSpentSeconds = Math.Max(0, request.TimeSpentSeconds);
        var calculatedWpm = text.WordCount > 0 && timeSpentSeconds > 0
            ? (int)(text.WordCount * 60d / timeSpentSeconds)
            : 0;
        var comprehensionRate = answers.Count > 0 && questions.Count > 0
            ? Math.Round(correctAnswers * 100m / questions.Count, 2)
            : Math.Clamp(request.ComprehensionScore, 0, 100);
        var efficiencyScore = calculatedWpm * (comprehensionRate / 100m);
        var now = DateTime.UtcNow;
        var session = ReadingSession.Import(
            Guid.NewGuid(),
            userId,
            textId,
            timeSpentSeconds,
            calculatedWpm,
            correctAnswers,
            questions.Count,
            comprehensionRate,
            efficiencyScore,
            now,
            now,
            userId.ToString(),
            null,
            null);
        db.ReadingSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new StudentReadingCompletion(
            session.Id,
            session.ReadingTimeSeconds,
            session.CalculatedWpm,
            session.CorrectAnswers,
            session.TotalQuestions,
            session.ComprehensionRate,
            session.EfficiencyScore,
            PerformanceLevel(session.CalculatedWpm));
    }

    public async Task<IReadOnlyList<StudentReadingHistoryItem>> GetHistoryAsync(
        Guid userId,
        Guid? readingTextId,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? category,
        CancellationToken cancellationToken) =>
        await GetHistoryAsync(userId, readingTextId, dateFrom, dateTo, category, true, cancellationToken);

    private async Task<IReadOnlyList<StudentReadingHistoryItem>> GetHistoryAsync(
        Guid userId,
        Guid? readingTextId,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? category,
        bool limitResults,
        CancellationToken cancellationToken)
    {
        var query = from session in db.ReadingSessions.AsNoTracking()
                    join text in db.ReadingTexts.AsNoTracking()
                        on session.ReadingTextId equals text.Id
                    where session.UserId == userId && !text.IsDeleted
                    select new { Session = session, Text = text };
        if (readingTextId.HasValue)
            query = query.Where(item => item.Session.ReadingTextId == readingTextId.Value);
        if (dateFrom.HasValue)
            query = query.Where(item => item.Session.CompletedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(item => item.Session.CompletedAt <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(item => item.Text.Category == category);

        var orderedQuery = query.OrderByDescending(item => item.Session.CompletedAt);
        var rows = limitResults
            ? await orderedQuery.Take(50).ToListAsync(cancellationToken)
            : await orderedQuery.ToListAsync(cancellationToken);
        return rows.Select(item => ToHistory(item.Session, item.Text)).ToList();
    }

    public async Task<StudentReadingSessionDetails?> GetSessionDetailsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && item.UserId == userId)
            .Select(item => new StudentReadingSessionDetails(
                item.Id,
                item.ReadingTextId,
                item.CalculatedWpm,
                item.ComprehensionRate,
                item.ReadingTimeSeconds,
                item.CompletedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<StudentReadingStatistics> GetStatisticsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var sessions = await GetHistoryAsync(userId, null, null, null, null, false, cancellationToken);
        if (sessions.Count == 0)
            return new StudentReadingStatistics(0, 0, 0, 0, 0, 0, [], []);

        return new StudentReadingStatistics(
            sessions.Count,
            Math.Round(sessions.Average(item => (decimal)item.CalculatedWPM), 1),
            Math.Round(sessions.Average(item => item.ComprehensionRate), 1),
            Math.Round(sessions.Average(item => item.EfficiencyScore), 1),
            sessions.Select(item => item.ReadingTextId).Distinct().Count(),
            sessions.Sum(item => item.ReadingTimeSeconds) / 60,
            sessions.Select(item => item.Category).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().OrderBy(item => item).ToList(),
            sessions.Take(10).ToList());
    }

    public async Task<IReadOnlyList<StudentReadingWpmPoint>> GetWpmProgressionAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.CompletedAt)
            .Select(item => new StudentReadingWpmPoint(item.CompletedAt, item.CalculatedWpm))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentReadingComprehensionPoint>> GetComprehensionProgressionAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.CompletedAt)
            .Select(item => new StudentReadingComprehensionPoint(item.CompletedAt, item.ComprehensionRate))
            .ToListAsync(cancellationToken);

    private async Task<int> GetCurrentLevelAsync(Guid userId, CancellationToken cancellationToken) =>
        Math.Clamp(await db.UserProfiles
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive)
            .Select(item => (int?)item.CurrentLevel)
            .SingleOrDefaultAsync(cancellationToken) ?? 1, 1, 10);

    private static StudentReadingHistoryItem ToHistory(ReadingSession session, ReadingText text) =>
        new(
            session.Id,
            session.ReadingTextId,
            text.Title,
            text.Category,
            session.ReadingTimeSeconds,
            session.CalculatedWpm,
            session.CorrectAnswers,
            session.TotalQuestions,
            session.ComprehensionRate,
            session.EfficiencyScore,
            session.CompletedAt,
            PerformanceLevel(session.CalculatedWpm));

    private static string PerformanceLevel(int wpm) => wpm switch
    {
        < 100 => "Başlangıç",
        < 200 => "Temel",
        < 300 => "Orta",
        < 400 => "İyi",
        _ => "İleri"
    };
}
