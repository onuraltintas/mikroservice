using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Application.Content;
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

        var questionRows = await db.ReadingQuestions
            .AsNoTracking()
            .Where(item => item.ReadingTextId == textId && !item.IsDeleted)
            .OrderBy(item => item.OrderIndex)
            .Select(item => new
            {
                Question = new StudentReadingQuestion(
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
                    item.OrderIndex),
                Snapshot = new ReadingQuestionScoringSnapshot(
                    item.Id,
                    item.Type,
                    item.BloomLevel,
                    item.OrderIndex,
                    item.CorrectAnswer)
            })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var attempt = StudentReadingAttempt.Start(
            Guid.NewGuid(),
            userId,
            textId,
            now);
        attempt.SetQuestionSnapshot(
            JsonSerializer.Serialize(questionRows.Select(item => item.Snapshot)),
            now);
        attempt.SetWordCountSnapshot(text.WordCount, now);
        db.StudentReadingAttempts.Add(attempt);

        await db.SaveChangesAsync(cancellationToken);

        return new StudentReadingStart(
            text.Id,
            attempt.Id,
            text.Title,
            text.Content,
            text.Category,
            text.DifficultyLevel,
            text.WordCount,
            questionRows.Select(item => item.Question).ToList());
    }

    public async Task<StudentReadingCompletion?> CompleteAsync(
        Guid userId,
        Guid textId,
        CompleteStudentReadingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId == Guid.Empty)
            throw new ArgumentException("A valid reading session is required.", nameof(request));

        var attempt = await db.StudentReadingAttempts
            .SingleOrDefaultAsync(item => item.Id == request.SessionId
                && item.UserId == userId
                && item.ReadingTextId == textId,
                cancellationToken);
        if (attempt is null)
            return null;

        var text = await db.ReadingTexts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == textId, cancellationToken);
        if (text is null)
            return null;

        var existingSession = await db.ReadingSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == attempt.Id && item.UserId == userId, cancellationToken);
        if (existingSession is not null)
            return ToCompletion(existingSession);

        var questions = ReadQuestionSnapshot(attempt.QuestionSnapshotJson)
            ?? throw new BusinessRuleException(
                "SpeedReading.ReadingSession.SnapshotMissing",
                "Bu okuma oturumu içerik kopyası olmadan başlatılmış. Lütfen metni yeniden başlatın.");
        var wordCount = attempt.WordCountSnapshot
            ?? throw new BusinessRuleException(
                "SpeedReading.ReadingSession.SnapshotMissing",
                "Bu okuma oturumunda metin ölçüm kopyası bulunamadı. Lütfen metni yeniden başlatın.");
        var answers = request.Answers ?? [];
        ValidateAnswers(questions.Select(item => item.Id).ToHashSet(), answers);
        var answerRows = answers
            .Join(questions, answer => answer.QuestionId, question => question.Id, (answer, question) => new
            {
                SelectedAnswer = NormalizeAnswer(answer.SelectedAnswer),
                Question = question,
                IsCorrect = string.Equals(
                    NormalizeAnswer(answer.SelectedAnswer),
                    question.CorrectAnswer?.Trim(),
                    StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
        var correctAnswers = answerRows.Count(item => item.IsCorrect);
        var timeSpentSeconds = CalculateServerDuration(attempt.StartedAt, DateTime.UtcNow);
        var calculatedWpm = wordCount > 0
            ? SpeedReadingExerciseSessionRules.CalculateValidatedRawWpm(wordCount, timeSpentSeconds) is { } rawWpm
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
            null,
            isMeasured: calculatedWpm > 0);
        attempt.Complete(now);
        db.ReadingSessions.Add(session);
        db.ReadingSessionAnswers.AddRange(answerRows.Select(item => ReadingSessionAnswer.Import(
            Guid.NewGuid(),
            session.Id,
            item.Question.Id,
            item.Question.Type,
            item.Question.BloomLevel,
            item.Question.OrderIndex,
            item.SelectedAnswer,
            item.IsCorrect,
            now,
            userId.ToString())));
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
        CancellationToken cancellationToken)
    {
        var session = await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && item.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
            return null;

        var answers = await db.ReadingSessionAnswers
            .AsNoTracking()
            .Where(item => item.SessionId == sessionId)
            .OrderBy(item => item.OrderIndex)
            .Select(item => new StudentReadingAnswerDetails(
                item.QuestionId,
                item.QuestionType,
                item.BloomLevel,
                item.OrderIndex,
                item.SelectedAnswer,
                item.IsCorrect))
            .ToListAsync(cancellationToken);

        return new StudentReadingSessionDetails(
            session.Id,
            session.ReadingTextId,
            session.CalculatedWpm,
            session.ComprehensionRate,
            session.ReadingTimeSeconds,
            session.CompletedAt,
            answers,
            session.IsMeasured);
    }

    public async Task<StudentReadingStatistics> GetStatisticsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var sessions = await GetHistoryAsync(userId, null, null, null, null, false, cancellationToken);
        if (sessions.Count == 0)
            return new StudentReadingStatistics(0, 0, 0, 0, 0, 0, [], []);

        var measuredSessions = sessions
            .Where(item => item.IsMeasured && item.CalculatedWPM > 0)
            .ToList();
        var comprehensionSessions = sessions.Where(item => item.TotalQuestions > 0).ToList();

        return new StudentReadingStatistics(
            sessions.Count,
            measuredSessions.Count == 0
                ? 0
                : Math.Round(measuredSessions.Average(item => (decimal)item.CalculatedWPM), 1),
            comprehensionSessions.Count == 0
                ? 0
                : Math.Round(comprehensionSessions.Average(item => item.ComprehensionRate), 1),
            measuredSessions.Count == 0
                ? 0
                : Math.Round(measuredSessions.Average(item => item.EfficiencyScore), 1),
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
            .Where(item => item.UserId == userId && item.IsMeasured && item.CalculatedWpm > 0)
            .OrderBy(item => item.CompletedAt)
            .Select(item => new StudentReadingWpmPoint(item.CompletedAt, item.CalculatedWpm))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentReadingComprehensionPoint>> GetComprehensionProgressionAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.TotalQuestions > 0)
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
            PerformanceLevel(session.CalculatedWpm),
            session.IsMeasured);

    private static StudentReadingCompletion ToCompletion(ReadingSession session) =>
        new(
            session.Id,
            session.ReadingTimeSeconds,
            session.CalculatedWpm,
            session.CorrectAnswers,
            session.TotalQuestions,
            session.ComprehensionRate,
            session.EfficiencyScore,
            PerformanceLevel(session.CalculatedWpm),
            session.IsMeasured);

    private static void ValidateAnswers(
        IReadOnlySet<Guid> questionIds,
        IReadOnlyList<StudentReadingAnswer> answers)
    {
        if (answers.Count != questionIds.Count)
            throw new ArgumentException("Every reading question must be answered.", nameof(answers));
        if (answers.Any(item => item.QuestionId == Guid.Empty || !IsSupportedAnswer(item.SelectedAnswer)))
            throw new ArgumentException("Every reading answer must contain a question and an A, B, C or D option (or be empty after timeout).", nameof(answers));
        if (answers.Select(item => item.QuestionId).Distinct().Count() != answers.Count)
            throw new ArgumentException("A reading question cannot be answered more than once.", nameof(answers));
        if (answers.Any(item => !questionIds.Contains(item.QuestionId)))
            throw new ArgumentException("An answer does not belong to the reading session.", nameof(answers));
    }

    private static bool IsSupportedAnswer(string? selectedAnswer) =>
        string.IsNullOrWhiteSpace(selectedAnswer)
        || selectedAnswer.Trim().ToUpperInvariant() is "A" or "B" or "C" or "D";

    private static string NormalizeAnswer(string selectedAnswer) =>
        string.IsNullOrWhiteSpace(selectedAnswer)
            ? ReadingSessionAnswer.TimeoutAnswer.ToUpperInvariant()
            : selectedAnswer.Trim().ToUpperInvariant();

    private static IReadOnlyList<ReadingQuestionScoringSnapshot>? ReadQuestionSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var snapshots = JsonSerializer.Deserialize<List<ReadingQuestionScoringSnapshot>>(json);
            if (snapshots is null
                || snapshots.Select(item => item.Id).Distinct().Count() != snapshots.Count
                || snapshots.Any(item => item.Id == Guid.Empty
                    || item.Type is < 1 or > 3
                    || item.BloomLevel is < 1 or > 6
                    || item.OrderIndex < 0
                    || !ReadingQuestionQualityRules.HasScorableAnswerKey(item.CorrectAnswer)))
            {
                throw new BusinessRuleException(
                    "SpeedReading.ReadingSession.SnapshotInvalid",
                    "Okuma oturumu içerik kopyası geçersiz. Lütfen metni yeniden başlatın.");
            }

            return snapshots;
        }
        catch (JsonException)
        {
            throw new BusinessRuleException(
                "SpeedReading.ReadingSession.SnapshotInvalid",
                "Okuma oturumu içerik kopyası geçersiz. Lütfen metni yeniden başlatın.");
        }
    }

    private sealed record ReadingQuestionScoringSnapshot(
        Guid Id,
        int Type,
        int BloomLevel,
        int OrderIndex,
        string CorrectAnswer);

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
