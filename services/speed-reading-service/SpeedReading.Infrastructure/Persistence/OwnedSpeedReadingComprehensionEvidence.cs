using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Comprehension evidence (H15, decision 30.10.2026). Only sessions that read a text and answered questions measure
/// comprehension; every other exercise (tachistoscope, Schulte, Hedef Avı, vocabulary…) records 0 and must not be
/// read as "understood nothing". Same rule as the exercise report (<c>OwnedSpeedReadingReportExercises</c>).
/// </summary>
internal static class OwnedSpeedReadingComprehensionEvidence
{
    public static readonly Expression<Func<ExerciseSessionResult, bool>> AskedQuestions =
        result => result.IsMeasured
            && !result.IsAssessmentMode
            && result.ReadingTextId != null
            && result.QuestionAnswersJson != "[]";

    public static async Task<decimal?> AverageAskedComprehensionAsync(
        OwnedSpeedReadingDbContext db,
        Guid studentId,
        List<Guid> sessionIds,
        CancellationToken cancellationToken)
    {
        if (sessionIds.Count == 0)
            return null;
        var scores = await db.ExerciseSessionResults.AsNoTracking()
            .Where(AskedQuestions)
            .Where(result => result.StudentId == studentId
                && result.SessionId != null
                && sessionIds.Contains(result.SessionId.Value))
            .Select(result => result.ComprehensionScore)
            .ToListAsync(cancellationToken);
        return scores.Count == 0 ? null : Math.Round(scores.Average(), 2);
    }
}
