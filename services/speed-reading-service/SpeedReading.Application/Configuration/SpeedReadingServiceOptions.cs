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
    /// Preserved in the capability response for existing clients. The service
    /// always uses its owned database.
    /// </summary>
    public bool OwnedDataEnabled { get; } = true;

    /// <summary>
    /// An exercise session that is still open (active or paused) after this many hours without any activity is
    /// closed as abandoned by the hourly clean-up.
    /// </summary>
    public int StaleSessionHours { get; set; } = 24;

    public void Validate()
    {
        if (Mode == SpeedReadingDeploymentMode.Standalone && CoachingIntegrationEnabled)
        {
            throw new InvalidOperationException(
                "CoachingIntegrationEnabled cannot be enabled in Standalone mode.");
        }

        if (StaleSessionHours < 1)
        {
            throw new InvalidOperationException("StaleSessionHours must be at least 1.");
        }
    }
}
