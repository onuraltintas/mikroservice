using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Application.Content;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Application.UnitTests;

public sealed class TrainingProgramSelectionTests
{
    [Fact]
    public async Task Training_excludes_assessment_only_even_when_its_id_sorts_first()
    {
        await using var db = Store();
        var type = ExerciseType.Create(Guid.NewGuid(), "Tachistoscope", "Tachistoscope", "text_stream");
        var normal = Exercise.Create("Normal", type.Name, "{}", 1, Guid.NewGuid(), type.Id,
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var assessment = Exercise.Create("Measurement", type.Name, "{}", 1, Guid.NewGuid(), type.Id,
            Guid.Parse("00000000-0000-0000-0000-000000000001"), isAssessmentOnly: true);
        db.ExerciseTypes.Add(type);
        db.Exercises.AddRange(normal, assessment);
        await db.SaveChangesAsync();

        var schedule = await Build(db, Pattern(type.Name));

        Selected(schedule).Should().Be(normal.Id);
    }

    [Theory]
    [InlineData("learning")]
    [InlineData("evaluation")]
    public async Task Comprehension_selects_the_requested_normal_mode(string mode)
    {
        await using var db = Store();
        var type = ExerciseType.Create(Guid.NewGuid(), "Comprehension", "Comprehension", "reading_comprehension");
        db.ExerciseTypes.Add(type);
        var wrong = Exercise.Create("Other mode", type.Name,
            Config(mode == "learning" ? "evaluation" : "learning"), 1, Guid.NewGuid(), type.Id,
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var desired = Exercise.Create("Chosen mode", type.Name, Config(mode), 1, Guid.NewGuid(), type.Id,
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        db.Exercises.AddRange(wrong, desired);
        await db.SaveChangesAsync();

        Selected(await Build(db, Pattern(type.Name, mode))).Should().Be(desired.Id);
    }

    [Fact]
    public async Task Missing_requested_mode_does_not_fall_back_to_the_other_mode()
    {
        await using var db = Store();
        var type = ExerciseType.Create(Guid.NewGuid(), "Comprehension", "Comprehension", "reading_comprehension");
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(Exercise.Create("Evaluation", type.Name, Config("evaluation"), 1, Guid.NewGuid(), type.Id));
        await db.SaveChangesAsync();
        var action = () => Build(db, Pattern(type.Name, "learning"));
        await action.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>();
    }

    [Theory]
    [InlineData("invalid", "Comprehension")]
    [InlineData("learning", "Fixation")]
    public void Invalid_or_unrelated_comprehension_mode_is_rejected(string mode, string type)
    {
        var action = () => ProgramWeeklyPatternRules.Validate(Pattern(type, mode), false, true);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Admin_update_preserves_assessment_ownership()
    {
        var actor = Guid.NewGuid();
        var type = Guid.NewGuid();
        var exercise = Exercise.Create("Measurement", "Fixation", "{}", 1, actor, type, isAssessmentOnly: true);
        exercise.Update("Renamed", "", "Fixation", "{}", 1, type, null, actor, DateTime.UtcNow);
        exercise.IsAssessmentOnly.Should().BeTrue();
    }

    private static OwnedSpeedReadingDbContext Store() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Normal_session_cannot_start_an_assessment_only_exercise()
    {
        await using var db = Store();
        var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Fixation", "motion_path");
        var exercise = Exercise.Create("Measurement", type.Name, "{}", 1, Guid.NewGuid(), type.Id, isAssessmentOnly: true);
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(serviceType, db)!;
        var action = () => service.StartAsync(Guid.NewGuid(), new StartExerciseSessionRequest { ExerciseId = exercise.Id });
        await action.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>()
            .Where(error => error.Code == "Exercise.AssessmentOnly");
    }

    [Fact]
    public async Task Student_catalog_excludes_assessment_content_but_admin_can_see_it()
    {
        await using var db = Store();
        var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Fixation", "motion_path");
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(Exercise.Create("Measurement", type.Name, "{}", 1, Guid.NewGuid(), type.Id, isAssessmentOnly: true));
        await db.SaveChangesAsync();
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingCatalog")!;
        var catalog = (ILegacySpeedReadingCatalog)Activator.CreateInstance(serviceType, db)!;
        (await catalog.GetExercisesAsync(null, null, null, 1, 20, null)).Items.Should().BeEmpty();
        (await catalog.GetExercisesAsync(null, null, null, 1, 20, null, includeInactive: true)).Items.Should().HaveCount(1);
    }

    private static string Pattern(string type, string? mode = null) => JsonSerializer.Serialize(new
    {
        week1 = new { day1 = new[] { new { type, count = 1, difficulty = 1, comprehensionMode = mode } } }
    });

    private static string Config(string mode) => JsonSerializer.Serialize(new
    {
        engineType = "reading_comprehension", engineConfig = new { comprehensionMode = mode, readingPurpose = "evaluation" }
    });

    private static Guid Selected(string schedule) => JsonDocument.Parse(schedule).RootElement[0].GetProperty("ExerciseId").GetGuid();

    private static async Task<string> Build(OwnedSpeedReadingDbContext db, string pattern)
    {
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Program", "", Guid.NewGuid(), 0, 100,
            pattern, 1, 0, 1, 1, 1, true, 1, 0, null, false, DateTime.UtcNow, null, null, null);
        var method = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramSchedule")!
            .GetMethod("BuildAsync", BindingFlags.Static | BindingFlags.Public)!;
        return await (Task<string>)method.Invoke(null, [db, template, null, CancellationToken.None])!;
    }
}
