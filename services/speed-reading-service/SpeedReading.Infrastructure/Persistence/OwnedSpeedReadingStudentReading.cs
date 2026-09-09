using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
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
                    || item.TargetAgeGroupId == null
                    || item.TargetAgeGroupId == ageGroupId.Value)
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
                    || item.TargetAgeGroupId == null
                    || item.TargetAgeGroupId == ageGroupId.Value));
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
        Guid userId,
        Guid textId,
        CancellationToken cancellationToken)
    {
        var ageGroupId = await GetAgeGroupIdAsync(userId, cancellationToken);
        var text = await db.ReadingTexts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == textId
                && item.IsActive
                && !item.IsDeleted
                && (!ageGroupId.HasValue
                    || item.TargetAgeGroupId == null
                    || item.TargetAgeGroupId == ageGroupId.Value), cancellationToken);
        if (text is null)
            return null;

        var attempt = StudentReadingAttempt.Start(
            Guid.NewGuid(),
            userId,
            textId,
            DateTime.UtcNow);
        db.StudentReadingAttempts.Add(attempt);

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
        if (text is null)
            return null;

        if (request.SessionId == Guid.Empty)
            throw new ArgumentException("A valid reading session is required.", nameof(request));

        var attempt = await db.StudentReadingAttempts
            .SingleOrDefaultAsync(item => item.Id == request.SessionId
                && item.UserId == userId
                && item.ReadingTextId == textId,
                cancellationToken);
        if (attempt is null)
            return null;

        var existingSession = await db.ReadingSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == attempt.Id && item.UserId == userId, cancellationToken);
        if (existingSession is not null)
            return ToCompletion(existingSession);

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
        var timeSpentSeconds = CalculateServerDuration(attempt.StartedAt, DateTime.UtcNow);
        var calculatedWpm = text.WordCount > 0
            ? SpeedReadingExerciseSessionRules.CalculateValidatedRawWpm(text.WordCount, timeSpentSeconds) is { } rawWpm
                ? (int)Math.Round(rawWpm)
                : 0
            : 0;
        var comprehensionRate = questions.Count > 0
            ? Math.Round(correctAnswers * 100m / questions.Count, 2)
            : 0;
        var efficiencyScore = calculatedWpm * (comprehensionRate / 100m);
        var now = DateTime.UtcNow;
        var session = ReadingSession.Import(
            attempt.Id,
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
        attempt.Complete(now);
        db.ReadingSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.ReadingSessions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == attempt.Id && item.UserId == userId, cancellationToken);
            if (concurrent is null)
                throw;

            return ToCompletion(concurrent);
        }

        return ToCompletion(session);
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

    private async Task<Guid?> GetAgeGroupIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserProfiles
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive)
            .Select(item => item.AgeGroupConfigurationId)
            .SingleOrDefaultAsync(cancellationToken);

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

    private static StudentReadingCompletion ToCompletion(ReadingSession session) =>
        new(
            session.Id,
            session.ReadingTimeSeconds,
            session.CalculatedWpm,
            session.CorrectAnswers,
            session.TotalQuestions,
            session.ComprehensionRate,
            session.EfficiencyScore,
            PerformanceLevel(session.CalculatedWpm));

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

    private static int CalculateServerDuration(DateTime startedAt, DateTime completedAt)
    {
        var seconds = (completedAt.ToUniversalTime() - startedAt.ToUniversalTime()).TotalSeconds;
        return (int)Math.Clamp(Math.Floor(seconds), 1, 86_400);
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
