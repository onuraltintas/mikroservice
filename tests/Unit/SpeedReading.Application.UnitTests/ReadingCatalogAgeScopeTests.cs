using System.Reflection;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ReadingCatalogAgeScopeTests
{
    [Theory]
    [InlineData("Chunking", "word_highlight")]
    [InlineData("RegressionReduction", "regression_reduction")]
    public async Task Wrong_age_catalogue_link_is_rejected_before_a_generic_text_can_hide_the_mismatch(string name, string engine)
    {
        await using var db = Store();
        var (user, exercise) = await Seed(db, name, engine, mismatch: true, withText: true);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).StartAsync(user, new() { ExerciseId = exercise }));
        Assert.Contains("yaş", error.Message.ToLowerInvariant());
        Assert.Empty(db.ExerciseSessions);
    }

    [Theory]
    [InlineData("Chunking", "word_highlight")]
    [InlineData("RegressionReduction", "regression_reduction")]
    public async Task Matching_profile_age_opens_the_existing_level_text(string name, string engine)
    {
        await using var db = Store();
        var (user, exercise) = await Seed(db, name, engine, mismatch: false, withText: true);
        var response = await Service(db).StartAsync(user, new() { ExerciseId = exercise });
        Assert.Equal(exercise, response.ExerciseId);
        Assert.NotNull((await db.ExerciseSessions.SingleAsync()).ReadingTextId);
    }

    [Theory]
    [InlineData("Chunking", "word_highlight")]
    [InlineData("RegressionReduction", "regression_reduction")]
    public async Task Truly_empty_level_returns_an_actionable_business_error_not_server_error(string name, string engine)
    {
        await using var db = Store();
        var (user, exercise) = await Seed(db, name, engine, mismatch: false, withText: false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).StartAsync(user, new() { ExerciseId = exercise }));
        Assert.Empty(db.ExerciseSessions);
    }

    private static OwnedSpeedReadingDbContext Store() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) => (ISpeedReadingExerciseSessions)Activator.CreateInstance(
        typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;

    private static async Task<(Guid user, Guid exercise)> Seed(OwnedSpeedReadingDbContext db, string name, string engine, bool mismatch, bool withText)
    {
        var user = Guid.NewGuid(); var age = Guid.NewGuid(); var selectedAge = mismatch ? Guid.NewGuid() : age;
        db.UserProfiles.Add(SpeedReadingUserProfile.Import(Guid.NewGuid(), user, 1, 150, 70, 15, age, null, true,
            DateTime.UtcNow, user.ToString(), null, null));
        var type = ExerciseType.Create(Guid.NewGuid(), name, name, engine);
        var configuration = JsonSerializer.Serialize(new { engineType = engine, engineConfig = new {
            mode = "chunking", readingPurpose = "practice", content = new { minWordCount = 10, maxWordCount = 100 },
            pacer = new { speedWpm = 200, chunkSize = 2 } } });
        var exercise = Exercise.Create(name, name, configuration, 1, user, type.Id, targetAgeGroupId: selectedAge);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        if (withText) db.ReadingTexts.Add(ReadingText.Create(Guid.NewGuid(), "Seviye metni", string.Join(" ", Enumerable.Repeat("kelime", 50)), difficultyLevel: 1));
        await db.SaveChangesAsync(); return (user, exercise.Id);
    }
}
