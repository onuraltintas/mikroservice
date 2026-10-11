using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Infrastructure.Persistence;

internal static class ReadingTextVisibility
{
    // Proofreading originals are answer material, not independent student reading content.
    public static IQueryable<ReadingText> IndependentReading(OwnedSpeedReadingDbContext db) => db.ReadingTexts.AsNoTracking()
        .Where(text => !db.Exercises.Any(exercise => exercise.Id == text.ExerciseId
            && db.ExerciseTypes.Any(type => type.Id == exercise.ExerciseTypeId
                && (type.Name == "ErrorAnalysis" || type.EngineType == "error_analysis"))));
}
