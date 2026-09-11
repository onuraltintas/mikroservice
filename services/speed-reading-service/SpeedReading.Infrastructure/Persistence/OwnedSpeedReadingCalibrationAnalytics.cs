using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingCalibrationAnalytics(OwnedSpeedReadingDbContext db)
    : ISpeedReadingCalibrationAnalytics
{
    public async Task<SpeedReadingCalibrationReport> GetAsync(CancellationToken cancellationToken)
    {
        var attempts = await db.AssessmentAttempts
            .AsNoTracking()
            .Where(item => item.Status == AssessmentAttemptStatus.Completed)
            .Select(item => new
            {
                item.Id,
                item.Phase,
                item.LevelCatalogVersion,
                item.AgeGroupConfigurationId,
                item.StudyCode,
                ProtocolVersion = item.StudyProtocolVersion,
                CohortCode = item.StudyCohortCode
            })
            .ToListAsync(cancellationToken);
        if (attempts.Count == 0)
            return Report([]);

        var attemptIds = attempts.Select(item => item.Id).ToArray();
        var resultRows = await db.ExerciseSessionResults
            .AsNoTracking()
            .Where(item => item.AssessmentAttemptId.HasValue
                && attemptIds.Contains(item.AssessmentAttemptId.Value)
                && item.IsAssessmentMode
                && item.IsMeasured)
            .Select(item => new
            {
                AttemptId = item.AssessmentAttemptId!.Value,
                item.ExerciseId,
                item.RawWpm,
                item.ComprehensionScore,
                item.CompletedAt
            })
            .ToListAsync(cancellationToken);
        var roles = await db.AssessmentAttemptExercises
            .AsNoTracking()
            .Where(item => attemptIds.Contains(item.AssessmentAttemptId))
            .ToDictionaryAsync(item => (item.AssessmentAttemptId, item.ExerciseId), item => item.Role, cancellationToken);
        var ageGroupIds = attempts
            .Where(item => item.AgeGroupConfigurationId.HasValue)
            .Select(item => item.AgeGroupConfigurationId!.Value)
            .Distinct()
            .ToArray();
        var ageGroups = await db.AgeGroupConfigurations
            .AsNoTracking()
            .Where(item => ageGroupIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);

        var observations = new List<SpeedReadingCalibrationObservation>();
        foreach (var attempt in attempts)
        {
            var latest = resultRows
                .Where(item => item.AttemptId == attempt.Id)
                .GroupBy(item => item.ExerciseId)
                .Select(group => group.OrderByDescending(item => item.CompletedAt).First())
                .ToArray();
            if (latest.Length == 0) continue;
            var wpm = latest.Where(item => item.RawWpm > 0).Select(item => item.RawWpm).ToArray();
            var comprehension = latest
                .Where(item => roles.GetValueOrDefault((attempt.Id, item.ExerciseId)) == "comprehension")
                .Select(item => Math.Clamp(item.ComprehensionScore, 0, 100))
                .ToArray();
            if (wpm.Length == 0 || comprehension.Length == 0) continue;
            var ageGroup = attempt.AgeGroupConfigurationId.HasValue
                ? ageGroups.GetValueOrDefault(attempt.AgeGroupConfigurationId.Value, "Tanımsız")
                : "Tanımsız";
            observations.Add(new SpeedReadingCalibrationObservation(
                attempt.Phase,
                attempt.LevelCatalogVersion,
                ageGroup,
                wpm.Average(),
                comprehension.Average(),
                attempt.StudyCode,
                attempt.ProtocolVersion,
                attempt.CohortCode));
        }

        return Report(SpeedReadingCalibrationRules.Summarize(observations));
    }

    private static SpeedReadingCalibrationReport Report(IReadOnlyList<SpeedReadingCalibrationSegment> segments) =>
        new(
            true,
            null,
            SpeedReadingCalibrationRules.MinimumPublishableSampleSize,
            DateTime.UtcNow,
            segments);
}
