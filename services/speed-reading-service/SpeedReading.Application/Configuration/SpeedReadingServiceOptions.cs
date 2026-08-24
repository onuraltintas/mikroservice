namespace SpeedReading.Application.Configuration;

public enum SpeedReadingDeploymentMode
{
    Standalone,
    Platform
}

public sealed class SpeedReadingServiceOptions
{
    public const string SectionName = "SpeedReading";

    public SpeedReadingDeploymentMode Mode { get; set; } = SpeedReadingDeploymentMode.Standalone;

    public bool CoachingIntegrationEnabled { get; set; }

    public bool NotificationIntegrationEnabled { get; set; }

    public bool SubscriptionIntegrationEnabled { get; set; }

    public void Validate()
    {
        if (Mode == SpeedReadingDeploymentMode.Standalone && CoachingIntegrationEnabled)
        {
            throw new InvalidOperationException(
                "CoachingIntegrationEnabled cannot be enabled in Standalone mode.");
        }
    }
}
