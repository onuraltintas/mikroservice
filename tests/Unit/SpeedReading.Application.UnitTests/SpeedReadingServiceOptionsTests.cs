using FluentAssertions;
using SpeedReading.Application.Configuration;

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
}
