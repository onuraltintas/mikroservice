using SpeedReading.Domain.Catalog;

namespace SpeedReading.Application.UnitTests;

public sealed class TrackingConfigurationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(60001)]
    public void RejectsUnsupportedCycleDuration(int speed)
    {
        Assert.Throws<ArgumentException>(() => ExerciseConfigurationRules.ValidateActiveConfiguration(
            $$$"""{"engineType":"motion_path","mode":"tracking","timing":{"speedMs":{{{speed}}}}}""", "motion_path"));
    }
}
