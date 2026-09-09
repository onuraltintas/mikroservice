using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.StudentReading;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingStudentReading(SpeedReadingDbContext db) : ISpeedReadingStudentReading
{
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentLevel = await db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId && !item.IsDeleted)
            .Select(item => (int?)item.CurrentLevel)
            .SingleOrDefaultAsync(cancellationToken) ?? 1;
        var ageGroupId = await GetAgeGroupIdAsync(userId, cancellationToken);
        var minLevel = Math.Max(1, currentLevel - 2);
        var maxLevel = Math.Min(10, currentLevel + 2);

        return await db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.IsActive
                && item.DifficultyLevel >= minLevel
                && item.DifficultyLevel <= maxLevel
                && (!ageGroupId.HasValue
                    || item.TargetAgeGroupConfigurationId == null
                    || item.TargetAgeGroupConfigurationId == ageGroupId.Value)
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
        var currentLevel = await db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId && !item.IsDeleted)
            .Select(item => (int?)item.CurrentLevel)
            .SingleOrDefaultAsync(cancellationToken) ?? 1;
        var ageGroupId = await GetAgeGroupIdAsync(userId, cancellationToken);
        var lowerLevel = Math.Clamp(minLevel ?? currentLevel - 2, 1, 10);
        var upperLevel = Math.Clamp(maxLevel ?? currentLevel + 2, lowerLevel, 10);

        var query = db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.IsActive
                && item.DifficultyLevel >= lowerLevel
                && item.DifficultyLevel <= upperLevel
                && (!ageGroupId.HasValue
                    || item.TargetAgeGroupConfigurationId == null
                    || item.TargetAgeGroupConfigurationId == ageGroupId.Value));
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(item => item.Category == category);
        if (specificLevel.HasValue) query = query.Where(item => item.DifficultyLevel == specificLevel.Value);

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
        Guid userId,
        Guid textId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid authenticated user is required.", nameof(userId));

        var ageGroupId = await GetAgeGroupIdAsync(userId, cancellationToken);
        var text = await db.ReadingTexts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == textId
                && item.IsActive
                && !item.IsDeleted
                && (!ageGroupId.HasValue
                    || item.TargetAgeGroupConfigurationId == null
                    || item.TargetAgeGroupConfigurationId == ageGroupId.Value), cancellationToken);
        if (text is null) return null;

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
                null,
                item.OptionA,
                item.OptionB,
                item.OptionC,
                item.OptionD,
                item.OrderIndex))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var attempt = new LegacyReadingSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReadingTextId = textId,
            ReadingTimeSeconds = 0,
            CalculatedWPM = 0,
            CorrectAnswers = 0,
            TotalQuestions = 0,
            ComprehensionRate = 0,
            EfficiencyScore = 0,
            CompletedAt = now,
            CreatedAt = now,
            CreatedBy = userId,
            IsDeleted = true,
            DeletedAt = null,
            DeletedBy = null
        };
        db.ReadingSessions.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);

        return new StudentReadingStart(
            text.Id,
            attempt.Id,
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
            .SingleOrDefaultAsync(item => item.Id == textId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (text is null) return null;

        if (request.SessionId == Guid.Empty)
            throw new ArgumentException("A valid reading session is required.", nameof(request));

        var session = await db.ReadingSessions
            .SingleOrDefaultAsync(item => item.Id == request.SessionId
                && item.UserId == userId
                && item.ReadingTextId == textId
                && (!item.IsDeleted
                    || (item.DeletedAt == null
                        && item.ReadingTimeSeconds == 0
                        && item.CalculatedWPM == 0
                        && item.TotalQuestions == 0)),
                cancellationToken);
        if (session is null)
            return null;
        if (!session.IsDeleted)
            return ToCompletion(session);

        var questions = await db.ReadingQuestions
            .AsNoTracking()
            .Where(item => item.ReadingTextId == textId && !item.IsDeleted)
            .Select(item => new { item.Id, item.CorrectAnswer })
            .ToListAsync(cancellationToken);
        var answers = request.Answers ?? [];
        ValidateAnswers(questions.Select(item => item.Id).ToHashSet(), answers);
        var correctAnswers = answers
            .Join(questions, answer => answer.QuestionId, question => question.Id, (answer, question) =>
                string.Equals(answer.SelectedAnswer?.Trim(), question.CorrectAnswer?.Trim(), StringComparison.OrdinalIgnoreCase))
            .Count(isCorrect => isCorrect);
        var now = DateTime.UtcNow;
        var timeSpentSeconds = CalculateServerDuration(session.CreatedAt, now);
        var calculatedWpm = text.WordCount > 0
            ? SpeedReadingExerciseSessionRules.CalculateValidatedRawWpm(text.WordCount, timeSpentSeconds) is { } rawWpm
                ? (int)Math.Round(rawWpm)
                : 0
            : 0;
        var comprehensionRate = questions.Count > 0
            ? Math.Round(correctAnswers * 100m / questions.Count, 2)
            : 0;
        session.ReadingTimeSeconds = timeSpentSeconds;
        session.CalculatedWPM = calculatedWpm;
        session.CorrectAnswers = correctAnswers;
        session.TotalQuestions = questions.Count;
        session.ComprehensionRate = comprehensionRate;
        session.EfficiencyScore = calculatedWpm * (comprehensionRate / 100m);
        session.CompletedAt = now;
        session.IsDeleted = false;
        session.UpdatedAt = now;
        session.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);

        return ToCompletion(session);
    }

    private static int CalculateServerDuration(DateTime startedAt, DateTime completedAt)
    {
        var seconds = (completedAt.ToUniversalTime() - startedAt.ToUniversalTime()).TotalSeconds;
        return (int)Math.Clamp(Math.Floor(seconds), 1, 86_400);
    }

    private static StudentReadingCompletion ToCompletion(LegacyReadingSession session) =>
        new(
            session.Id,
            session.ReadingTimeSeconds,
            session.CalculatedWPM,
            session.CorrectAnswers,
            session.TotalQuestions,
            session.ComprehensionRate,
            session.EfficiencyScore,
            PerformanceLevel(session.CalculatedWPM));

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
                    where session.UserId == userId && !session.IsDeleted && !text.IsDeleted
                    select new { Session = session, Text = text };
        if (readingTextId.HasValue) query = query.Where(item => item.Session.ReadingTextId == readingTextId.Value);
        if (dateFrom.HasValue) query = query.Where(item => item.Session.CompletedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(item => item.Session.CompletedAt <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(item => item.Text.Category == category);

        var orderedQuery = query.OrderByDescending(item => item.Session.CompletedAt);
        var rows = limitResults
            ? await orderedQuery.Take(50).ToListAsync(cancellationToken)
            : await orderedQuery.ToListAsync(cancellationToken);
        return rows.Select(item => ToHistory(item.Session, item.Text)).ToList();
    }

    public async Task<StudentReadingSessionDetails?> GetSessionDetailsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && item.UserId == userId && !item.IsDeleted)
            .Select(item => new StudentReadingSessionDetails(
                item.Id,
                item.ReadingTextId,
                item.CalculatedWPM,
                item.ComprehensionRate,
                item.ReadingTimeSeconds,
                item.CompletedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<StudentReadingStatistics> GetStatisticsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var sessions = await GetHistoryAsync(userId, null, null, null, null, false, cancellationToken);
        if (sessions.Count == 0)
        {
            return new StudentReadingStatistics(0, 0, 0, 0, 0, 0, [], []);
        }

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
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .OrderBy(item => item.CompletedAt)
            .Select(item => new StudentReadingWpmPoint(item.CompletedAt, item.CalculatedWPM))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentReadingComprehensionPoint>> GetComprehensionProgressionAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .OrderBy(item => item.CompletedAt)
            .Select(item => new StudentReadingComprehensionPoint(item.CompletedAt, item.ComprehensionRate))
            .ToListAsync(cancellationToken);

    private static StudentReadingHistoryItem ToHistory(LegacyReadingSession session, LegacyReadingText text) =>
        new(
            session.Id,
            session.ReadingTextId,
            text.Title,
            text.Category,
            session.ReadingTimeSeconds,
            session.CalculatedWPM,
            session.CorrectAnswers,
            session.TotalQuestions,
            session.ComprehensionRate,
            session.EfficiencyScore,
            session.CompletedAt,
            PerformanceLevel(session.CalculatedWPM));

    private async Task<Guid?> GetAgeGroupIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId && !item.IsDeleted)
            .Select(item => item.AgeGroupConfigurationId)
            .SingleOrDefaultAsync(cancellationToken);

    private static void ValidateAnswers(
        IReadOnlySet<Guid> questionIds,
        IReadOnlyList<StudentReadingAnswer> answers)
    {
        if (answers.Count != questionIds.Count)
            throw new ArgumentException("Every reading question must be answered.", nameof(answers));
        if (answers.Any(item => item.QuestionId == Guid.Empty || string.IsNullOrWhiteSpace(item.SelectedAnswer)))
            throw new ArgumentException("Every reading answer must contain a question and a selected option.", nameof(answers));
        if (answers.Select(item => item.QuestionId).Distinct().Count() != answers.Count)
            throw new ArgumentException("A reading question cannot be answered more than once.", nameof(answers));
        if (answers.Any(item => !questionIds.Contains(item.QuestionId)))
            throw new ArgumentException("An answer does not belong to the reading session.", nameof(answers));
    }

    private static string PerformanceLevel(int wpm) => wpm switch
    {
        < 100 => "Başlangıç",
        < 200 => "Temel",
        < 300 => "Orta",
        < 400 => "İyi",
        _ => "İleri"
    };
}
