using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Reads the authoritative result produced by an owned exercise session.
/// Client-supplied performance metrics are deliberately never persisted here.
/// </summary>
internal sealed class OwnedSpeedReadingProgressWriter(OwnedSpeedReadingDbContext db)
    : ISpeedReadingProgressWriter
{
    public async Task<ExerciseResultSummary> CreateExerciseResultAsync(
        Guid studentId,
        CreateExerciseResultRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        SpeedReadingProgressWriteRules.ValidateIdempotencyKey(idempotencyKey);
        ValidateRequest(studentId, request);
        var result = await db.ExerciseSessionResults
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.SessionId == request.SessionId
                && item.StudentId == studentId,
                cancellationToken);
        if (result is null)
            throw new NotFoundException("ExerciseSessionResult", request.SessionId!.Value);
        if (result.ExerciseId != request.ExerciseId
            || (request.ReadingTextId.HasValue && result.ReadingTextId != request.ReadingTextId))
        {
            throw new BusinessRuleException(
                "ExerciseResult.SessionMismatch",
                "The submitted session does not belong to the requested exercise or reading text.");
        }

        return ToSummary(result);
    }

    private static void ValidateRequest(
        Guid studentId,
        CreateExerciseResultRequest request)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("A valid authenticated student is required.", nameof(studentId));
        if (request.ExerciseId == Guid.Empty)
            throw new ArgumentException("ExerciseId is required.", nameof(request));
        SpeedReadingProgressWriteRules.RequireAuthoritativeSession(request.SessionId);
    }

    private static ExerciseResultSummary ToSummary(ExerciseSessionResult result) => new(
        result.Id,
        result.ExerciseId,
        result.ReadingTextId,
        result.WordsRead,
        result.TimeSpentSeconds,
        result.IsMeasured && result.RawWpm > 0 ? result.RawWpm : null,
        result.IsMeasured && result.ReadingTextId.HasValue ? result.ComprehensionScore : null,
        result.IsMeasured && result.RawWpm > 0 ? result.WeightedKdp : null,
        result.CompletedAt,
        result.IsMeasured ? "Measured" : "NotMeasured");
}
