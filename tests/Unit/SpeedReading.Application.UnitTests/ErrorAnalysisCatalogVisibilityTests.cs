using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpeedReading.API.Controllers;
using SpeedReading.Application.Content;
using SpeedReading.Application.StudentReading;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class ErrorAnalysisCatalogVisibilityTests
{
    [Fact]
    public async Task Student_catalog_cannot_reveal_proofreading_originals_but_other_texts_remain_available()
    {
        await using var db = Context(); var (user, exercise, text, catalog) = await Seed(db);
        Assert.Null(await catalog.GetReadingTextAsync(text.Id, true, false, false, user));
        Assert.Empty(await catalog.GetReadingTextsAsync(exercise.Id, null, null, null, false, null, true, user));
        Assert.DoesNotContain(await catalog.GetShortReadingTextsAsync(10, user), item => item.Id == text.Id);
        Assert.DoesNotContain("proofreading-fixture", await catalog.GetReadingTextCategoriesAsync(user));
        Assert.DoesNotContain(4, await catalog.GetReadingTextDifficultyLevelsAsync(user));
        Assert.NotNull(await catalog.GetReadingTextAsync(text.Id, true, false, true, null));
        var reading = (ISpeedReadingStudentReading)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentReading")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        Assert.Null(await reading.StartAsync(user, text.Id, CancellationToken.None));
        Assert.DoesNotContain(await reading.GetAvailableTextsAsync(user, null, null, null, null, CancellationToken.None), item => item.Id == text.Id);
        var ordinary = ReadingText.Create(Guid.NewGuid(), "Normal metin", "Okuma metni.", difficultyLevel: 2);
        db.ReadingTexts.Add(ordinary); await db.SaveChangesAsync();
        Assert.NotNull(await catalog.GetReadingTextAsync(ordinary.Id, false, false, false, user));
    }

    [Theory]
    [InlineData("Student", false)]
    [InlineData("Teacher", true)]
    [InlineData("Admin", true)]
    [InlineData("Teacher,Student", false)]
    public async Task Read_only_staff_preview_does_not_grant_student_answer_access(string roleNames, bool allowed)
    {
        await using var db = Context(); var (user, exercise, text, catalog) = await Seed(db);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()) }
            .Concat(roleNames.Split(',').Select(role => new Claim(ClaimTypes.Role, role))), "fixture");
        var context = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        var exercises = new ExercisesController(catalog, null!) { ControllerContext = context };
        var result = Assert.IsType<OkObjectResult>((await exercises.GetExercise(exercise.Id)).Result);
        var config = Assert.IsType<ExerciseSummary>(result.Value).ConfigurationJson;
        Assert.Equal(allowed, config.Contains("errorsByTextId", StringComparison.Ordinal));
        var texts = new ReadingTextsController(catalog, null!, null!) { ControllerContext = context };
        var details = (await texts.GetReadingText(text.Id)).Result;
        if (allowed) Assert.IsType<OkObjectResult>(details);
        else Assert.IsType<NotFoundResult>(details);
    }

    private static OwnedSpeedReadingDbContext Context() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<(Guid, Exercise, ReadingText, ILegacySpeedReadingCatalog)> Seed(OwnedSpeedReadingDbContext db)
    {
        var user = Guid.NewGuid(); var age = Guid.NewGuid(); var type = ExerciseType.Create(Guid.NewGuid(), "ErrorAnalysis", "Hata Analizi", "error_analysis");
        var exercise = Exercise.Create("Hata Analizi", "ErrorAnalysis",
            """{"engineType":"error_analysis","engineConfig":{"errorsByTextId":{"fixture":[{"originalWord":"yalnız"}]}}}""",
            4, user, type.Id, targetAgeGroupId: age);
        var text = ReadingText.Create(Guid.NewGuid(), "Özgün", "yalnız bugün", category: "proofreading-fixture", difficultyLevel: 4,
            exerciseId: exercise.Id, targetAgeGroupId: age);
        db.UserProfiles.Add(SpeedReadingUserProfile.Import(Guid.NewGuid(), user, 4, 200, 70, 15, age, null, true, DateTime.UtcNow, null, null, null));
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.ReadingTexts.Add(text); await db.SaveChangesAsync();
        var catalog = (ILegacySpeedReadingCatalog)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingCatalog")!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
        return (user, exercise, text, catalog);
    }
}
