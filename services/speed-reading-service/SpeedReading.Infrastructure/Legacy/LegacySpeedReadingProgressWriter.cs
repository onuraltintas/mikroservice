using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;

namespace SpeedReading.Infrastructure.Legacy;

/// <summary>
/// Compatibility adapter for installations that still use the legacy schema.
/// The old endpoint accepted client-calculated metrics, so it now only exposes
/// results already produced by a server-owned exercise session.
/// </summary>
internal sealed class LegacySpeedReadingProgressWriter(SpeedReadingDbContext db)
    : ISpeedReadingProgressWriter
{
    public async Task<ExerciseResultSummary> CreateExerciseResultAsync(
        Guid studentId,
        CreateExerciseResultRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("A valid authenticated student is required.", nameof(studentId));
        }

        if (request.ExerciseId == Guid.Empty)
        {
            throw new ArgumentException("ExerciseId is required.", nameof(request));
        }

        SpeedReadingProgressWriteRules.ValidateIdempotencyKey(idempotencyKey);
        var sessionId = SpeedReadingProgressWriteRules.RequireAuthoritativeSession(request.SessionId);
        var result = await db.StudentExerciseResults
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.SessionId == sessionId
                && item.StudentId == studentId
                && item.ExerciseId == request.ExerciseId
                && (!request.ReadingTextId.HasValue || item.ReadingTextId == request.ReadingTextId)
                && !item.IsDeleted,
                cancellationToken);

        if (result is null)
        {
            throw new KeyNotFoundException("The completed server-owned exercise session was not found.");
        }

        return ToSummary(result);
    }

    private static ExerciseResultSummary ToSummary(LegacyStudentExerciseResult result) => new(
        result.Id,
        result.ExerciseId,
        result.ReadingTextId,
        result.WordsRead,
        result.TimeSpentSeconds,
        result.IsMeasured && result.RawWPM > 0 ? result.RawWPM : null,
        result.IsMeasured && result.ReadingTextId.HasValue ? result.ComprehensionScore : null,
        result.IsMeasured && result.RawWPM > 0 ? result.WeightedKDP : null,
        result.CompletedAt,
        result.IsMeasured ? "Measured" : "NotMeasured");
}
