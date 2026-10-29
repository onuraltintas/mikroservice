using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.AdaptiveText;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H3 (finding 13): adaptive text suggestions never hand out the original (answer) text of an error-analysis
/// exercise, and they follow the student's level and age group instead of the most-understood texts overall.
/// </summary>
public sealed class SpeedReadingAdaptiveTextVisibilityTests
{
    [Fact]
    public async Task Suggestions_hide_answer_texts_and_follow_the_students_level_and_age_group()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        var ageGroup = Guid.NewGuid();
        var otherAgeGroup = Guid.NewGuid();
        db.UserProfiles.Add(SpeedReadingUserProfile.Import(Guid.NewGuid(), student, 7, 250, 70m, 20, ageGroup, null, true,
            DateTime.UtcNow, null, null, null));
        var type = ExerciseType.Create(Guid.NewGuid(), "ErrorAnalysis", "Hata Analizi", "error_analysis");
        var exercise = Exercise.Create("Hata Analizi", "error_analysis", "{}", 7, student, type.Id, targetAgeGroupId: ageGroup);
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(exercise);
        db.ReadingTexts.Add(Text("Cevap metni", 7, ageGroup, 99m, exercise.Id));
        for (var index = 0; index < 30; index++)
            db.ReadingTexts.Add(Text($"Kolay {index}", 1, ageGroup, 95m));
        db.ReadingTexts.Add(Text("Başka yaş", 7, otherAgeGroup, 90m));
        db.ReadingTexts.Add(Text("Uygun", 7, ageGroup, 60m));
        db.ReadingTexts.Add(Text("Yakın", 8, null, 55m));
        await db.SaveChangesAsync();
        var service = (ISpeedReadingAdaptiveText)Activator.CreateInstance(
            typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAdaptiveText")!, db)!;

        var suggestions = await service.GetRecommendationsAsync(student, 2, null);

        suggestions.Select(item => item.Title).Should().Equal("Uygun", "Yakın");
    }

    private static ReadingText Text(string title, int level, Guid? ageGroup, decimal comprehension, Guid? exerciseId = null) =>
        ReadingText.Import(Guid.NewGuid(), title, $"{title} içeriği burada.", "tr", exerciseId, 120, "Genel", level, ageGroup, true,
            null, 1, 10, comprehension, 3, 60, DateTime.UtcNow, null, null, null);
}
