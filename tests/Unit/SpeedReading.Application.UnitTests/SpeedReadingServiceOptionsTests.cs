using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EduPlatform.Shared.Infrastructure.Middleware;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.AgeGroups;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.Content;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Gamification;
using SpeedReading.Application.Progress;
using SpeedReading.Application.Configuration;
using SpeedReading.Application.StudentProgram;
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
        options.OwnedDataEnabled.Should().BeTrue();
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
    public void Runtime_requires_only_the_owned_connection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SpeedReadingOwned"] = "Host=owned;Database=owned"
            })
            .Build();
        var services = new ServiceCollection();

        var action = () => services.AddSpeedReadingInfrastructure(configuration);

        action.Should().NotThrow();
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
    public void Runtime_does_not_register_a_legacy_database_context()
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

        services.Should().NotContain(item => item.ServiceType.Name == "SpeedReadingDbContext");
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
    public void Owned_data_mode_resolves_programs_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ILegacySpeedReadingPrograms))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingPrograms");
    }

    [Fact]
    public void Owned_data_mode_resolves_student_program_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ISpeedReadingStudentProgram))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingStudentProgram");
    }

    [Fact]
    public void Owned_data_mode_resolves_assessment_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ISpeedReadingAssessment))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingAssessment");
    }

    [Fact]
    public void Owned_data_mode_resolves_age_groups_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ISpeedReadingAgeGroups))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingAgeGroups");
    }

    [Fact]
    public void Owned_data_mode_resolves_program_admin_writes_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ISpeedReadingProgramAdminWriter))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingProgramAdminWriter");
    }

    [Fact]
    public void Owned_data_mode_resolves_daily_progress_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ISpeedReadingDailyProgress))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingDailyProgress");
    }

    [Fact]
    public void Owned_data_mode_resolves_catalog_reads_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ILegacySpeedReadingCatalog))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingCatalog");
    }

    [Fact]
    public void Owned_data_mode_resolves_catalog_writes_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ISpeedReadingCatalogAdminWriter))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingCatalogAdminWriter");
    }

    [Fact]
    public void Owned_data_mode_resolves_learning_paths_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ILegacySpeedReadingLearningPaths))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingLearningPaths");
    }

    [Fact]
    public void Owned_data_mode_resolves_learning_path_writes_from_the_owned_store()
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
            .Last(item => item.ServiceType == typeof(ISpeedReadingLearningPathAdminWriter))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingLearningPathAdminWriter");
    }

    [Fact]
    public void Owned_data_mode_resolves_cross_cutting_writes_from_the_owned_store()
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

        services.Last(item => item.ServiceType == typeof(IAdminAuditWriter))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingAdminAuditWriter");
        services.Last(item => item.ServiceType == typeof(ISpeedReadingIdempotencyCleaner))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingIdempotencyCleaner");
    }

    [Fact]
    public void Owned_data_mode_resolves_gamification_from_the_owned_store()
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

        services.Last(item => item.ServiceType == typeof(ILegacySpeedReadingGamification))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingGamification");
        services.Last(item => item.ServiceType == typeof(ISpeedReadingGamificationAdminWriter))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingGamificationAdminWriter");
    }

    [Fact]
    public void Owned_data_mode_resolves_progress_reads_and_writes_from_the_owned_store()
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

        services.Last(item => item.ServiceType == typeof(ILegacySpeedReadingProgress))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingProgress");
        services.Last(item => item.ServiceType == typeof(ISpeedReadingProgressWriter))
            .ImplementationType!
            .Name
            .Should()
            .Be("OwnedSpeedReadingProgressWriter");
    }

}