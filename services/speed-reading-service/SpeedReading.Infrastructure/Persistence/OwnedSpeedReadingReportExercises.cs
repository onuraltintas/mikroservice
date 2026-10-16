using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

// A saved result is authoritative. Older daily records without a linked result remain reportable.
internal static class OwnedSpeedReadingReportExercises
{
    internal static IQueryable<ReportedExercise> All(OwnedSpeedReadingDbContext db)
    {
        var results = from result in db.ExerciseSessionResults.AsNoTracking()
            join exercise in db.Exercises.AsNoTracking() on result.ExerciseId equals exercise.Id into exercises
            from exercise in exercises.DefaultIfEmpty()
            join type in db.ExerciseTypes.AsNoTracking() on exercise.ExerciseTypeId equals type.Id into types
            from type in types.DefaultIfEmpty()
            where !result.IsAssessmentMode
            select new ReportedExercise {
                UserId = result.StudentId, SessionId = result.SessionId ?? result.LegacySessionId,
                ExerciseId = result.ExerciseId, ExerciseTypeId = exercise == null ? Guid.Empty : exercise.ExerciseTypeId,
                Title = exercise == null ? "Egzersiz" : exercise.Title,
                TypeName = type == null ? "Egzersiz" : type.DisplayName == "" ? type.Name : type.DisplayName,
                DifficultyLevel = exercise == null ? 0 : exercise.DifficultyLevel,
                CompletedDate = result.CompletedAt, TimeSpentSeconds = result.TimeSpentSeconds,
                SuccessRate = result.Score, IsMeasured = result.IsMeasured,
                IsPassed = result.IsMeasured && result.Score >= 60,
                AverageWPM = result.IsMeasured && result.RawWpm > 0 ? result.RawWpm : null,
                AverageComprehension = result.IsMeasured && result.ReadingTextId != null && result.QuestionAnswersJson != "[]"
                    ? result.ComprehensionScore : null
            };
        var legacy = from log in db.DailyExerciseLogs.AsNoTracking()
            join exercise in db.Exercises.AsNoTracking() on log.ExerciseId equals exercise.Id into exercises
            from exercise in exercises.DefaultIfEmpty()
            join type in db.ExerciseTypes.AsNoTracking() on log.ExerciseTypeId equals type.Id into types
            from type in types.DefaultIfEmpty()
            where !log.SessionId.HasValue || !db.ExerciseSessionResults.Any(result => result.StudentId == log.UserId
                && (result.SessionId == log.SessionId || result.LegacySessionId == log.SessionId))
            select new ReportedExercise {
                UserId = log.UserId, SessionId = log.SessionId, ExerciseId = log.ExerciseId,
                ExerciseTypeId = log.ExerciseTypeId, Title = exercise == null ? "Egzersiz" : exercise.Title,
                TypeName = type == null ? "Egzersiz" : type.DisplayName == "" ? type.Name : type.DisplayName,
                DifficultyLevel = log.DifficultyLevel, CompletedDate = log.CompletedDate, TimeSpentSeconds = log.TimeSpentSeconds,
                SuccessRate = log.SuccessRate, IsMeasured = log.IsMeasured, IsPassed = log.IsMeasured && log.IsPassed,
                AverageWPM = log.IsMeasured && log.AverageWPM > 0 ? log.AverageWPM : null,
                AverageComprehension = log.IsMeasured ? log.AverageComprehension : null
            };
        return results.Concat(legacy);
    }

    internal static IQueryable<ReportedExercise> WithoutReadingDuplicates(OwnedSpeedReadingDbContext db) =>
        All(db).Where(row => !row.SessionId.HasValue || !db.ReadingSessions.Any(reading =>
            reading.UserId == row.UserId && reading.Id == row.SessionId));
}

internal sealed class ReportedExercise
{
    public Guid UserId { get; init; }
    public Guid? SessionId { get; init; }
    public Guid ExerciseId { get; init; }
    public Guid ExerciseTypeId { get; init; }
    public string Title { get; init; } = "";
    public string TypeName { get; init; } = "";
    public int DifficultyLevel { get; init; }
    public DateTime CompletedDate { get; init; }
    public int TimeSpentSeconds { get; init; }
    public decimal SuccessRate { get; init; }
    public bool IsMeasured { get; init; }
    public bool IsPassed { get; init; }
    public decimal? AverageWPM { get; init; }
    public decimal? AverageComprehension { get; init; }
}
