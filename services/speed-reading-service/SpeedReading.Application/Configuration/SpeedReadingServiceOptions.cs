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

    /// <summary>
    /// Switches the core exercise/session slice from the legacy compatibility
    /// store to the owned Speed Reading database. It remains off until
    /// backfill and parity evidence are approved.
    /// </summary>
    public bool OwnedDataEnabled { get; set; }

    public void Validate()
    {
        if (Mode == SpeedReadingDeploymentMode.Standalone && CoachingIntegrationEnabled)
        {
            throw new InvalidOperationException(
                "CoachingIntegrationEnabled cannot be enabled in Standalone mode.");
        }
    }
}
