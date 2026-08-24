using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingProgressWriter(SpeedReadingDbContext db) : ISpeedReadingProgressWriter
{
    private const string IdempotencyScope = "speed-reading.exercise-results.create";
    private const int MaxJsonLength = 256 * 1024;

    public async Task<ExerciseResultSummary> CreateExerciseResultAsync(
        Guid studentId,
        CreateExerciseResultRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        idempotencyKey = idempotencyKey?.Trim() ?? string.Empty;
        ValidateRequest(studentId, request, idempotencyKey);

        // Bind the idempotency payload to the authenticated student. A key
        // copied by another user must never replay the first user's result.
        var requestHash = SpeedReadingRequestHasher.Create(studentId, request);
        var existing = await db.IdempotencyRecords
            .SingleOrDefaultAsync(item => item.Scope == IdempotencyScope && item.Key == idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            return await ReplayAsync(existing, studentId, requestHash, cancellationToken);
        }

        await EnsureReferencesExistAsync(request, cancellationToken);

        var now = DateTime.UtcNow;
        var result = new LegacyStudentExerciseResult
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ExerciseId = request.ExerciseId,
            ReadingTextId = request.ReadingTextId,
            WordsRead = request.WordsRead,
            TimeSpentSeconds = request.TimeSpentSeconds,
            RawWPM = request.RawWpm,
            ComprehensionScore = request.ComprehensionScore,
            WeightedKDP = request.WeightedKdp,
            QuestionAnswersJson = request.QuestionAnswersJson,
            ReadingMovementsJson = request.ReadingMovementsJson,
            CompletedAt = request.CompletedAt?.ToUniversalTime() ?? now,
            CreatedAt = now,
            CreatedBy = studentId,
            IsDeleted = false
        };

        db.StudentExerciseResults.Add(result);
        db.IdempotencyRecords.Add(new LegacyIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = IdempotencyScope,
            Key = idempotencyKey,
            RequestHash = requestHash,
            ResourceId = result.Id,
            CreatedAt = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Another request with the same key won the race. The ledger is the
            // source of truth for replay, so discard the losing tracked inserts.
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords
                .SingleAsync(item => item.Scope == IdempotencyScope && item.Key == idempotencyKey, cancellationToken);
            return await ReplayAsync(concurrent, studentId, requestHash, cancellationToken);
        }

        return ToSummary(result);
    }

    private async Task<ExerciseResultSummary> ReplayAsync(
        LegacyIdempotencyRecord record,
        Guid studentId,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if (!record.Matches(requestHash))
        {
            throw new BusinessRuleException(
                "Idempotency.Conflict",
                "The idempotency key was already used with a different request payload.");
        }

        var result = await db.StudentExerciseResults
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId
                && item.StudentId == studentId
                && !item.IsDeleted,
                cancellationToken);

        if (result is null)
        {
            throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "The idempotency record points to a missing exercise result.");
        }

        return ToSummary(result);
    }

    private async Task EnsureReferencesExistAsync(
        CreateExerciseResultRequest request,
        CancellationToken cancellationToken)
    {
        var exerciseExists = await db.Exercises
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.ExerciseId && !item.IsDeleted, cancellationToken);

        if (!exerciseExists)
        {
            throw new NotFoundException("Exercise", request.ExerciseId);
        }

        if (request.ReadingTextId is null)
        {
            return;
        }

        var readingTextExists = await db.ReadingTexts
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.ReadingTextId
                && item.ExerciseId == request.ExerciseId
                && !item.IsDeleted,
                cancellationToken);

        if (!readingTextExists)
        {
            throw new NotFoundException("ReadingText", request.ReadingTextId.Value);
        }
    }

    private static void ValidateRequest(
        Guid studentId,
        CreateExerciseResultRequest request,
        string idempotencyKey)
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("A valid authenticated student is required.", nameof(studentId));
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                idempotencyKey ?? string.Empty,
                "^[A-Za-z0-9._~-]{16,128}$"))
        {
            throw new ArgumentException(
                "Idempotency-Key must contain 16-128 letters, numbers, dots, underscores, hyphens or tildes.",
                nameof(idempotencyKey));
        }

        if (request.ExerciseId == Guid.Empty)
        {
            throw new ArgumentException("ExerciseId is required.", nameof(request));
        }

        if (request.WordsRead < 0 || request.TimeSpentSeconds <= 0
            || request.RawWpm < 0 || request.ComprehensionScore is < 0 or > 100
            || request.WeightedKdp < 0)
        {
            throw new ArgumentException("Exercise metrics contain an invalid value.", nameof(request));
        }

        ValidateJson(request.QuestionAnswersJson, nameof(request.QuestionAnswersJson));
        ValidateJson(request.ReadingMovementsJson, nameof(request.ReadingMovementsJson));

        if (request.CompletedAt.HasValue
            && request.CompletedAt.Value.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            throw new ArgumentException("CompletedAt cannot be in the future.", nameof(request));
        }
    }

    private static void ValidateJson(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxJsonLength)
        {
            throw new ArgumentException("A non-empty JSON payload up to 256 KB is required.", parameterName);
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
            {
                throw new ArgumentException("The JSON payload must be an object or array.", parameterName);
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The JSON payload is invalid.", parameterName, exception);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == "23505"
        && string.Equals(
            postgresException.ConstraintName,
            "UX_SpeedReadingIdempotencyRecords_Scope_Key",
            StringComparison.Ordinal);

    private static ExerciseResultSummary ToSummary(LegacyStudentExerciseResult result) => new(
        result.Id,
        result.ExerciseId,
        result.ReadingTextId,
        result.WordsRead,
        result.TimeSpentSeconds,
        result.RawWPM,
        result.ComprehensionScore,
        result.WeightedKDP,
        result.CompletedAt);
}
