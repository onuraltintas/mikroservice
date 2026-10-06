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

        var exerciseType = await (from exercise in db.Exercises.AsNoTracking()
            join type in db.ExerciseTypes.AsNoTracking() on exercise.ExerciseTypeId equals type.Id
            where exercise.Id == result.ExerciseId
            select type).SingleOrDefaultAsync(cancellationToken);
        var isVisualization = exerciseType is not null
            && (exerciseType.EngineType.Equals("visualization", StringComparison.OrdinalIgnoreCase)
                || exerciseType.Name.Contains("visualization", StringComparison.OrdinalIgnoreCase)
                || exerciseType.Name.Contains("visualisation", StringComparison.OrdinalIgnoreCase));
        var isFocus = exerciseType is not null
            && (exerciseType.EngineType.Equals("focus", StringComparison.OrdinalIgnoreCase)
                || exerciseType.EngineType.Equals("attention_training", StringComparison.OrdinalIgnoreCase)
                || exerciseType.EngineType.Equals("motion_path", StringComparison.OrdinalIgnoreCase)
                || exerciseType.Name.Contains("focus", StringComparison.OrdinalIgnoreCase)
                || exerciseType.Name.Contains("attention", StringComparison.OrdinalIgnoreCase)
                || exerciseType.Name.Contains("fixation", StringComparison.OrdinalIgnoreCase));
        return ToSummary(result, isVisualization || isFocus);
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

    private static ExerciseResultSummary ToSummary(ExerciseSessionResult result, bool suppressReadingMetrics) => new(
        result.Id,
        result.ExerciseId,
        result.ReadingTextId,
        suppressReadingMetrics ? 0 : result.WordsRead,
        result.TimeSpentSeconds,
        !suppressReadingMetrics && result.IsMeasured && result.RawWpm > 0 ? result.RawWpm : null,
        result.IsMeasured && result.ReadingTextId.HasValue ? result.ComprehensionScore : null,
        !suppressReadingMetrics && result.IsMeasured && result.RawWpm > 0 ? result.WeightedKdp : null,
        result.CompletedAt,
        result.IsMeasured ? "Measured" : "NotMeasured");
}
