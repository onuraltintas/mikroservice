using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Configuration;
using SpeedReading.Infrastructure;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingServiceOptionsTests
{
    [Fact]
    public void Defaults_to_standalone_without_optional_integrations()
    {
        var options = new SpeedReadingServiceOptions();

        options.Mode.Should().Be(SpeedReadingDeploymentMode.Standalone);
        options.CoachingIntegrationEnabled.Should().BeFalse();
        options.NotificationIntegrationEnabled.Should().BeFalse();
        options.SubscriptionIntegrationEnabled.Should().BeFalse();
    }

    [Fact]
    public void Standalone_mode_rejects_coaching_integration()
    {
        var options = new SpeedReadingServiceOptions
        {
            Mode = SpeedReadingDeploymentMode.Standalone,
            CoachingIntegrationEnabled = true
        };

        var action = () => options.Validate();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*CoachingIntegrationEnabled*");
    }

    [Fact]
    public void Platform_mode_can_enable_optional_integrations()
    {
        var options = new SpeedReadingServiceOptions
        {
            Mode = SpeedReadingDeploymentMode.Platform,
            CoachingIntegrationEnabled = true,
            NotificationIntegrationEnabled = true,
            SubscriptionIntegrationEnabled = true
        };

        var action = () => options.Validate();

        action.Should().NotThrow();
    }

    [Fact]
    public void Legacy_content_model_uses_existing_tables_without_creating_a_new_schema()
    {
        var options = new DbContextOptionsBuilder<SpeedReadingDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new SpeedReadingDbContext(options);
        var tables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null)
            .ToArray();

        tables.Should().Contain(new[]
        {
            "ExerciseTypes",
            "Exercises",
            "ReadingTexts",
            "ReadingQuestions",
            "ExerciseSessions",
            "StudentExerciseResults",
            "ReadingSessions",
            "ExerciseProgramTemplates",
            "StudentProgramProgresses",
            "DailyExerciseLogs"
        });
        context.Database.ProviderName.Should().Contain("Npgsql");

        var readingText = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "ReadingTexts");
        readingText.FindProperty("Tags")!.GetColumnType().Should().Be("text");

        var exerciseResult = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "StudentExerciseResults");
        exerciseResult.FindProperty("RawWPM")!.GetColumnType().Should().Be("numeric(18,2)");

        var exerciseSession = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "ExerciseSessions");
        exerciseSession.FindProperty("Status")!.GetColumnType().Should().Be("integer");
    }
}
