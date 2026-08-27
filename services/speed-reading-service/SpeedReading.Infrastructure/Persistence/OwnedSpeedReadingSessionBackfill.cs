using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingSessionBackfillResult(
    int SessionsInserted,
    int AnswersInserted,
    int ResultsInserted,
    int ReadingSessionsInserted,
    int OrphanResults,
    int ExistingRows,
    DateTime CompletedAtUtc);

/// <summary>
/// Copies historical session, answer, result and reading-history rows after
/// the owned catalog has been backfilled. Legacy session references that no
/// longer have a source session are retained in LegacySessionId instead of
/// being silently discarded.
/// </summary>
public sealed class OwnedSpeedReadingSessionBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OwnedSpeedReadingSessionBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await owned.Database.MigrateAsync(cancellationToken);

        var sessions = await legacy.ExerciseSessions
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var results = await legacy.StudentExerciseResults
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var readingSessions = await legacy.ReadingSessions
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var ownedExerciseIds = await owned.Exercises
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var ownedReadingTextIds = await owned.ReadingTexts
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        ValidateReferences(sessions, results, readingSessions, ownedExerciseIds, ownedReadingTextIds);

        var resultBySessionId = results
            .Where(item => item.SessionId.HasValue)
            .GroupBy(item => item.SessionId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var sourceSessionIds = sessions.Select(item => item.Id).ToHashSet();

        await using var transaction = await owned.Database.BeginTransactionAsync(cancellationToken);
        var existingRows = 0;
        var sessionsInserted = 0;
        var answersInserted = 0;

        var existingSessionIds = await owned.ExerciseSessions
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in sessions)
        {
            if (existingSessionIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            if (!Enum.IsDefined(typeof(ExerciseSessionStatus), source.Status))
            {
                throw new InvalidOperationException(
                    $"Exercise session {source.Id} contains unknown status {source.Status}.");
            }

            var totalSteps = Math.Max(source.TotalSteps, 1);
            var timeLimitSeconds = source.TimeLimitSeconds is > 0
                ? source.TimeLimitSeconds
                : null;
            var session = ExerciseSession.Import(
                source.Id,
                source.StudentId,
                source.ExerciseId,
                source.ReadingTextId,
                source.StudentAssignmentId,
                (ExerciseSessionStatus)source.Status,
                NormalizeUtc(source.StartTime),
                NormalizeUtc(source.EndTime),
                Math.Max(source.TotalPausedSeconds, 0),
                NormalizeUtc(source.PausedAt),
                timeLimitSeconds,
                source.CurrentStep,
                totalSteps,
                Math.Max(source.CorrectCount, 0),
                Math.Max(source.IncorrectCount, 0),
                source.SessionDataJson,
                source.CustomDataJson,
                source.ProcessedActionsJson,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy));

            var answerSources = ReadAnswers(source, resultBySessionId.GetValueOrDefault(source.Id));
            foreach (var answer in answerSources)
            {
                session.ImportAnswer(ExerciseSessionAnswer.Import(
                    Guid.NewGuid(),
                    source.Id,
                    answer.QuestionId,
                    answer.Answer,
                    answer.IsCorrect,
                    answer.TimeSpentSeconds,
                    answer.BloomLevel));
                answersInserted++;
            }

            owned.ExerciseSessions.Add(session);
            sessionsInserted++;
        }

        var existingResultIds = await owned.ExerciseSessionResults
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var orphanResults = 0;
        var resultsInserted = 0;
        foreach (var source in results)
        {
            if (existingResultIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            var linkedSessionId = source.SessionId.HasValue && sourceSessionIds.Contains(source.SessionId.Value)
                ? source.SessionId
                : null;
            if (source.SessionId.HasValue && !linkedSessionId.HasValue)
                orphanResults++;

            owned.ExerciseSessionResults.Add(ExerciseSessionResult.Import(
                source.Id,
                linkedSessionId,
                source.StudentId,
                source.ExerciseId,
                source.ReadingTextId,
                Math.Max(source.WordsRead, 0),
                Math.Max(source.TimeSpentSeconds, 0),
                Math.Max(source.RawWPM, 0),
                Math.Clamp(source.ComprehensionScore, 0, 100),
                Math.Max(source.WeightedKDP, 0),
                Math.Clamp(source.WeightedKDP, 0, 100),
                NormalizeUtc(source.CompletedAt),
                source.QuestionAnswersJson,
                source.ReadingMovementsJson,
                source.SessionId,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            resultsInserted++;
        }

        var existingReadingSessionIds = await owned.ReadingSessions
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var readingSessionsInserted = 0;
        foreach (var source in readingSessions)
        {
            if (existingReadingSessionIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.ReadingSessions.Add(ReadingSession.Import(
                source.Id,
                source.UserId,
                source.ReadingTextId,
                Math.Max(source.ReadingTimeSeconds, 0),
                Math.Max(source.CalculatedWPM, 0),
                Math.Max(source.CorrectAnswers, 0),
                Math.Max(source.TotalQuestions, 0),
                Math.Clamp(source.ComprehensionRate, 0, 100),
                Math.Max(source.EfficiencyScore, 0),
                NormalizeUtc(source.CompletedAt),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            readingSessionsInserted++;
        }

        await owned.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OwnedSpeedReadingSessionBackfillResult(
            sessionsInserted,
            answersInserted,
            resultsInserted,
            readingSessionsInserted,
            orphanResults,
            existingRows,
            DateTime.UtcNow);
    }

    private static void ValidateReferences(
        IReadOnlyList<LegacyExerciseSession> sessions,
        IReadOnlyList<LegacyStudentExerciseResult> results,
        IReadOnlyList<LegacyReadingSession> readingSessions,
        IReadOnlySet<Guid> exerciseIds,
        IReadOnlySet<Guid> readingTextIds)
    {
        foreach (var session in sessions)
        {
            if (!exerciseIds.Contains(session.ExerciseId))
                throw new InvalidOperationException($"Session {session.Id} references missing exercise {session.ExerciseId}.");
            if (session.ReadingTextId.HasValue && !readingTextIds.Contains(session.ReadingTextId.Value))
                throw new InvalidOperationException($"Session {session.Id} references missing reading text {session.ReadingTextId}.");
        }

        foreach (var result in results)
        {
            if (!exerciseIds.Contains(result.ExerciseId))
                throw new InvalidOperationException($"Result {result.Id} references missing exercise {result.ExerciseId}.");
            if (result.ReadingTextId.HasValue && !readingTextIds.Contains(result.ReadingTextId.Value))
                throw new InvalidOperationException($"Result {result.Id} references missing reading text {result.ReadingTextId}.");
        }

        foreach (var readingSession in readingSessions)
        {
            if (!readingTextIds.Contains(readingSession.ReadingTextId))
                throw new InvalidOperationException(
                    $"Reading session {readingSession.Id} references missing reading text {readingSession.ReadingTextId}.");
        }
    }

    private static IReadOnlyList<ImportedAnswer> ReadAnswers(
        LegacyExerciseSession session,
        LegacyStudentExerciseResult? result)
    {
        var answers = DeserializeAnswers(session.SessionDataJson);
        if (answers.Count == 0 && result is not null)
            answers = DeserializeAnswers(result.QuestionAnswersJson);

        var duplicate = answers
            .GroupBy(item => item.QuestionId)
            .FirstOrDefault(group => group.Key == Guid.Empty || group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Session {session.Id} contains duplicate or empty question answers.");
        }

        return answers;
    }

    private static List<ImportedAnswer> DeserializeAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            var state = JsonSerializer.Deserialize<ImportedSessionState>(json, JsonOptions);
            if (state?.Answers is { Count: > 0 })
                return state.Answers;
            return JsonSerializer.Deserialize<List<ImportedAnswer>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ToAuditValue(Guid? value) => value?.ToString();

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue ? NormalizeUtc(value.Value) : null;

    private sealed class ImportedSessionState
    {
        public List<ImportedAnswer> Answers { get; set; } = [];
    }

    private sealed class ImportedAnswer
    {
        public Guid QuestionId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int TimeSpentSeconds { get; set; }
        public int BloomLevel { get; set; }
    }
}
