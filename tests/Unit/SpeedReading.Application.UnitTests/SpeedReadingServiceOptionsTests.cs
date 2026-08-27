using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.ExerciseSessions;
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
        options.OwnedDataEnabled.Should().BeFalse();
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
    public void Owned_data_mode_requires_a_separate_connection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SpeedReading"] = "Host=legacy;Database=legacy",
                ["SpeedReading:OwnedDataEnabled"] = "true"
            })
            .Build();
        var services = new ServiceCollection();

        var action = () => services.AddSpeedReadingInfrastructure(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*OwnedDataEnabled*");
    }

    [Fact]
    public void Owned_data_mode_resolves_sessions_from_the_owned_store()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SpeedReading"] = "Host=legacy;Database=legacy",
                ["ConnectionStrings:SpeedReadingOwned"] = "Host=owned;Database=owned",
                ["SpeedReading:OwnedDataEnabled"] = "true"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddSpeedReadingInfrastructure(configuration);

        services
            .Last(item => item.ServiceType == typeof(ISpeedReadingExerciseSessions))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingExerciseSessions");
    }

    [Fact]
    public void Owned_data_mode_resolves_assignments_from_the_owned_store()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SpeedReading"] = "Host=legacy;Database=legacy",
                ["ConnectionStrings:SpeedReadingOwned"] = "Host=owned;Database=owned",
                ["SpeedReading:OwnedDataEnabled"] = "true"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddSpeedReadingInfrastructure(configuration);

        services
            .Last(item => item.ServiceType == typeof(ISpeedReadingAssignments))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingAssignments");
    }

    [Fact]
    public void Legacy_content_model_preserves_existing_tables_and_adds_only_the_write_ledger()
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
            "DailyExerciseLogs",
            "LearningPathTemplates",
            "LearningPathNodes",
            "NodeContents",
            "NodePrerequisites",
            "StudentPathProgresses",
            "StudentNodeProgresses",
            "PersonalizedLearningPaths",
            "SpeedReadingIdempotencyRecords",
            "SpeedReadingAdminAuditRecords"
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
        exerciseSession.FindProperty("ProcessedActionsJson")!.IsNullable.Should().BeFalse();
        exerciseSession.FindProperty("TimeLimitSeconds")!.IsNullable.Should().BeTrue();

        exerciseResult.FindProperty("SessionId")!.IsNullable.Should().BeTrue();
        exerciseResult.GetIndexes()
            .Should().Contain(index => index.IsUnique
                && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "SessionId" }));

        var dailyExerciseLog = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "DailyExerciseLogs");
        dailyExerciseLog.FindProperty("ResultDataJson")!.IsNullable.Should().BeFalse();
        dailyExerciseLog.FindProperty("AverageResponseTimeMs")!.GetColumnType()
            .Should().Be("numeric(18,2)");
        dailyExerciseLog.FindProperty("TimeOfDay")!.GetColumnType()
            .Should().Be("interval");

        var gamification = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "UserGameifications");
        gamification.FindProperty("MaxWPM").Should().NotBeNull();
        gamification.FindProperty("MaxComprehensionScore")!.GetColumnType()
            .Should().Be("numeric(18,2)");

        var idempotencyLedger = context.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "SpeedReadingIdempotencyRecords");
        idempotencyLedger.GetIndexes()
            .Should().Contain(index => index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { "Scope", "Key" }));
    }
}
